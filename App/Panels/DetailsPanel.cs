﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using SatisfactorySavegameTool.Dialogs;

using CoreLib;

using Savegame;
using Savegame.Properties;
using P = Savegame.Properties;

/*
 * TODO:
 * 
 */
namespace SatisfactorySavegameTool.Panels
{

	public class DetailsPanel : StackPanel
	{
		internal static readonly string EMPTY = Translate._("DetailsPanel.Empty");

		public DetailsPanel()
			: base()
		{ }


		public void ShowProperty(Property prop)
		{
			_ClearAll();

			Log.Info("Adding property {0}", prop);
			Expando exp;
			if (prop == null)
			{
				exp = _Add(null, EMPTY, prop);
				exp.IsEnabled = false;
			}
			else
			{
				exp = _Add(null, null, prop);
				exp.IsExpanded = true;
			}
			Children.Add(exp);
		}


		internal void _ClearAll()
		{
			Children.Clear();
		}

		internal Expando _Add(Expando parent, string name, Property prop)
		{
			string label;
			ValueControl ctrl;

			if (prop != null)
			{
				// Those are to be moved into explicit type handlers???
				if (prop is ArrayProperty)
				{
					ArrayProperty array_p = prop as ArrayProperty;
					if (array_p.Name.ToString() == "mFogOfWarRawData")
					{
						parent.AddRow(new ImageControl(array_p.Name.ToString(), (byte[]) array_p.Value));
						return parent;
					}
					//...more to come? We'll see
				}

				// Those are to be moved into explicit type handlers???
				if (prop is StructProperty)
				{
					StructProperty struct_p = prop as StructProperty;
					if (struct_p.Value != null && struct_p.Index == 0 && !struct_p.IsArray)
					{
						bool process = (struct_p.Unknown == null) || (struct_p.Unknown.Length == 0);
						if (!process)
						{
							int sum = 0;
							foreach(byte b in struct_p.Unknown)
								sum += b;
							process = (sum == 0);
						}
						if (process)
						{
							// Replace it with type of actual value
							/*TODO:
							t = prop.Value.TypeName
							if t in globals():
								cls = globals()[t]
								cls(parent_pane, parent_sizer, prop.Name, prop.Value)
								return parent_pane, parent_sizer
							*/
							ctrl = ControlFactory.Create(struct_p.Name.ToString(), struct_p.Value as Property);
							if (ctrl != null)
							{
								parent.AddRow(ctrl);
								return parent;
							}
						}
					}
				}

				if (prop is ObjectProperty)
				{
					ObjectProperty obj_p = prop as ObjectProperty;

					// Have seen 4 different combinations so far:
					// - (1) Only PathName valid, LevelName + Name + Value = empty (sub in an ArrayProperty)
					// - (2) PathName + LevelName, but Name + Value are empty (also with ArrayProperty)
					// - (2) PathName + Name valid, but LevelName + Value empty (sub in a StructProperty)
					// - (3) PathName, LevelName + Name, but empty Value (sub in an EntityObj)
					//
					//=> PathName  LevelName  Name  Value
					//      x          -       -      -
					//      x          x       -      -
					//      x          -       x      -
					//      x          x       x      -

					if (str.IsNull(obj_p.LevelName) || obj_p.LevelName.ToString() == "Persistent_Level")
					{
						if (str.IsNull(obj_p.Name))
						{
							// Only PathName (... are we in an ArrayProperty?)
							UIElement single = ControlFactory.Create(obj_p.PathName);
							parent.AddRow(single);
						}
						else
						{
							// PathName + Name, so Name is our label
							ctrl = ControlFactory.CreateSimple(obj_p.Name.ToString(), obj_p.PathName);
							parent.AddRow(ctrl);
						}
						return parent;
					}
				}

				/*TODO:
				t = prop.TypeName
				if t in globals():
					cls = globals()[t]
					cls(parent_pane, parent_sizer, name, prop)
					return parent_pane, parent_sizer
				*/
				ctrl = ControlFactory.Create(name, prop);
				if (ctrl != null)
				{
					parent.AddRow(ctrl);
					return parent;
				}
			}

			label = (prop != null) ? prop.ToString() : name;
			Expando exp = new Expando(parent, label);

			if (prop != null)
			{
				Dictionary<string,object> childs = prop.GetChilds();
				if (childs.Count == 0)
					exp.IsEnabled = false;
				else
					_AddRecurs(exp, childs);
			}

			return exp;
		}

		internal void _AddRecurs(Expando parent, Dictionary<string,object> childs)
		{
			// Sort children first by both their "type" and name
			var names = childs.Keys.OrderBy((s) => s);
			List<string> simple = new List<string>();
			List<string> simple2 = new List<string>();
			List<string> props = new List<string>();
			List<string> sets = new List<string>();
			List<string> last = new List<string>();
			foreach (string name in names)
			{
				object sub = childs[name];
				if (sub is System.Collections.ICollection)//isinstance(sub, (list,dict)):
				{
					if (name == "Missing")
						last.Add(name);
					else if (name == "Unknown")//and isinstance(sub, list)):
						last.Add(name);
					else
						sets.Add(name);
				}
				else if (sub is Property)
				{
					//Property prop = sub as Property;
					//if (prop.TypeName in globals)
					//if (sub is Entity)
					//	simple2.Add(name);
					//else
					//	props.Add(name);
					if (sub is Entity)
						sets.Add(name);
					else if (sub is ValueProperty)						
						simple2.Add(name);
					else
						props.Add(name);
				}
				else
					simple.Add(name);
			}
			List<string> order = new List<string>();
			order.AddRange(simple);
			order.AddRange(simple2);
			order.AddRange(props);
			order.AddRange(sets);
			order.AddRange(last);

			foreach (string name in order)//childs.Keys)
			{
				object sub = childs[name];
				Log.Info("_AddRecurs: {0}", sub != null ? sub.GetType() : sub);

				//TODO: Those are to be moved into explicit type handlers???
				// Do some testing on property name here as some do need special handling, e.g.
				// - Length : Must be readonly as this will be calculated based on properties stored.
				// - Missing: Should show a (readonly) hex dump
				if (name == "Length")
				{
					parent.AddRow(new ReadonlySimpleValueControl<int>(name, (int)sub));
				}
				else if (name == "Missing")
				{
					parent.AddRow(new HexdumpControl(name, sub as byte[]));
				}
				else if (name == "Unknown")// && (sub is Array || sub is System.Collections.IEnumerable))
				{
					// There are several .Unknown properties, dump only list-based ones
					parent.AddRow(new HexdumpControl(name, sub as byte[]));
				}
				else if (name == "WasPlacedInLevel" || name == "NeedTransform")
				{
					parent.AddRow(name, new BoolControl((int)sub));
				}
				else if (sub is System.Collections.IDictionary)
				{
					System.Collections.IDictionary e = sub as System.Collections.IDictionary;
					string label = string.Format("{0} [{1}]", name, e.Count);
					Expando sub_exp = _Add(parent, label, null);
					if (e.Count == 0)
						sub_exp.IsEnabled = false;
					else
						foreach (object key in e.Keys)
						{
							object obj = e[key];

							if (obj is Property)
							{
								label = key.ToString();obj.ToString();
								_Add(sub_exp, label, (Property)obj);
							}
							//else?
						}
				}
				else if (sub is System.Collections.ICollection)
				{
					System.Collections.ICollection e = sub as System.Collections.ICollection;
					string label = string.Format("{0} [{1}]", name, e.Count);
					Expando sub_exp = _Add(parent, label, null);
					if (e.Count == 0)
						sub_exp.IsEnabled = false;
					else
						foreach (object obj in e)
						{
							if (obj is Property)
							{
								label = obj.ToString();
								_Add(sub_exp, label, (Property)obj);
							}
							//else?
						}
				}
				else if (sub is Property)
				{
					_Add(parent, name, sub as Property);
				}
				else
				{
					parent.AddRow(ControlFactory.CreateSimple(name, sub));
				}
			}

		}
	}

	internal class Expando : Expander
	{
		internal Expando(Expando parent, string label)
		{
			Header = label;
			HorizontalContentAlignment = HorizontalAlignment.Stretch;

			_grid = new Grid();
			_grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength( 0, GridUnitType.Auto ) });
			_grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength( 1, GridUnitType.Star ) });
			_grid.Margin = new Thickness(10, 4, 5, 4);//LTRB
			_grid.Background = Brushes.Transparent;

			Border b = new Border() {
				BorderBrush = Brushes.DarkGray,
				BorderThickness = new Thickness(1, 0, 0, 1),//LTRB
				Margin = new Thickness(10, 0, 0, 0),//LTRB
			};
			b.Child = _grid;
			Content = b;

			if (parent != null)
				parent.AddRow(this);
		}

		internal void AddRow(UIElement element)
		{
			int index = _AddRow();

			Grid.SetRow(element, index);
			Grid.SetColumn(element, 0);
			Grid.SetColumnSpan(element, 2);
			_grid.Children.Add(element);
		}

		internal void AddRow(string label, UIElement element)
		{
			int index = _AddRow();

			Label ctrl = new Label() {
				Content = label + ":",
				HorizontalAlignment = HorizontalAlignment.Left,
				VerticalAlignment = VerticalAlignment.Stretch,
				VerticalContentAlignment = VerticalAlignment.Center,
			};
			Thickness t = ctrl.Padding;
			t.Top = t.Bottom = 0;
			ctrl.Padding = t;
			Grid.SetRow(ctrl, index);
			Grid.SetColumn(ctrl, 0);
			_grid.Children.Add(ctrl);

			Grid.SetRow(element, index);
			Grid.SetColumn(element, 1);
			_grid.Children.Add(element);
		}

		internal void AddRow(ValueControl vc)
		{
			AddRow(vc.Label, vc.Ctrl);
		}

		internal int _AddRow()
		{
			RowDefinition rowdef = new RowDefinition() {
				Height = new GridLength(0, GridUnitType.Auto),
			};
			_grid.RowDefinitions.Add(rowdef);
			return _grid.RowDefinitions.Count - 1;
		}

		internal Grid _grid;
	}


	// Every control must implement this getter/setter pattern
	internal interface IValueContainer<_ValueType>
	{
		_ValueType Value { get; set; }
	}


	// Basic controls avail
	// 
	// Those will not only take care of displaying value correctly,
	// but will also take care of validating user input.
	// All controls MUST follow getter/setter pattern by supplying
	// both a Set() and Get() method.
	// 
	//TODO: Add validators to keep values in feasible limit

	internal class BoolControl : CheckBox, IValueContainer<int>
	{
		internal BoolControl(int val)
			: base()
		{
			HorizontalAlignment = HorizontalAlignment.Left;
			VerticalAlignment = VerticalAlignment.Center;
			Margin = new Thickness(0, 4, 0, 4);
			Value = val;
		}

		public int Value
		{
			get { return (IsChecked.GetValueOrDefault() ? 1 : 0); }
			set { IsChecked = (value != 0); }
		}
	}

	internal class FloatControl : TextBox, IValueContainer<float>
	{
		internal readonly string _format = "{0:F7}"; //TODO: Translate._("");

		internal FloatControl(float val)
			: base()
		{
			Width = new GridLength((Math.Abs(val) > 1e7f) ? 200 : 100).Value;
			HorizontalAlignment = HorizontalAlignment.Left;
			TextAlignment = TextAlignment.Right;
			Value = val;
		}

		public float Value
		{
			get
			{
				float f;
				if (!float.TryParse(Text, out f))
					throw new FormatException("Input for float value is invalid"); //TODO: Translate._("");
				return f;
			}
			set { Text = string.Format(_format, value); }
		}
	}

	internal class ByteControl : TextBox, IValueContainer<byte> // Might change to wx.SpinCtrl later
	{
		internal readonly string _format = "{0}"; //TODO: Translate._("");

		internal ByteControl(byte val)
			: base()
		{
			Width = new GridLength(50).Value;
			HorizontalAlignment = HorizontalAlignment.Left;
			TextAlignment = TextAlignment.Right;
			Value = val;
		}

		public byte Value
		{
			get
			{
				byte b;
				if (!byte.TryParse(Text, out b))
					throw new FormatException("Input for byte value is invalid"); //TODO: Translate._("");
				return b;
			}
			set	{ Text = string.Format(_format, value); }
		}
	}

	internal class IntControl : TextBox, IValueContainer<int> // Might change to wx.SpinCtrl later
	{
		internal readonly string _format = "{0:#,#0}"; //TODO: Translate._("");

		internal IntControl(int val)
			: base()
		{
			Width = new GridLength((Math.Abs(val) > 1e10) ? 200 : 100).Value;
			HorizontalAlignment = HorizontalAlignment.Left;
			TextAlignment = TextAlignment.Right;
			Value = val;
		}

		public int Value
		{
			get
			{
				int i;
				if (!int.TryParse(Text, out i))
					throw new FormatException("Input for integer value is invalid"); //TODO: Translate._("");
				return i;
			}
			set	{ Text = string.Format(_format, value); }
		}
	}

	internal class LongControl : TextBox, IValueContainer<long> // Might change to wx.SpinCtrl later
	{
		internal readonly string _format = "{0:#,#0}"; //TODO: Translate._("");

		internal LongControl(long val)
			: base()
		{
			Width = new GridLength(200).Value;
			HorizontalAlignment = HorizontalAlignment.Left;
			TextAlignment = TextAlignment.Right;
			Value = val;
		}

		public long Value
		{
			get
			{
				long l;
				if (!long.TryParse(Text, out l))
					throw new FormatException("Input for long integer value is invalid"); //TODO: Translate._("");
				return l;
			}
			set	{ Text = string.Format(_format, value); }
		}
	}

	internal class StrControl : TextBox, IValueContainer<str>
	{
		internal StrControl(str val)
			: base()
		{
			HorizontalAlignment = HorizontalAlignment.Stretch;
			VerticalAlignment = VerticalAlignment.Stretch;
			Value = val;
		}

		public str Value
		{
			get { return (Text != DetailsPanel.EMPTY) ? new str(Text) : null; }
			set	{ Text = (value != null) ? value.ToString() : DetailsPanel.EMPTY; }
		}
	}

	/*
	class ColorControl(wx.ColourPickerCtrl):
		"""
		Shows current color, opening wx.ColourDialog if clicked
		"""
		def __init__(self, parent, val:wx.Colour):
			super().__init__(parent)
			#self.Disable()# Let's hope this won't ruin visualization
			self.Unbind(wx.EVT_BUTTON)
			self.Set(val)

		def Set(self, val): self.Colour = val
		def Get(self):      return self.Colour
	# As disabling will change color to gray, we will use a simple 
	# display until we've added modifying savegames and saving.
	 */
	internal class ColorDisplay : Label, IValueContainer<Savegame.Properties.Color>
	{
		internal ColorDisplay(Savegame.Properties.Color color)
		{
			Content = "";
			Width = new GridLength(100).Value;
			BorderBrush = System.Windows.Media.Brushes.DarkGray;
			BorderThickness = new Thickness(1);
			Value = color;
		}

		public Savegame.Properties.Color Value
		{
			get { return _value; }
			set {
				_value = value;
				System.Windows.Media.Color c = 
					System.Windows.Media.Color.FromArgb(value.A, value.R, value.G, value.B);
				Background = new SolidColorBrush(c);
			}
		}

		internal Savegame.Properties.Color _value;
	}

	internal class LinearColorDisplay : Label, IValueContainer<LinearColor>
	{
		internal LinearColorDisplay(LinearColor color)
		{
			Content = "";
			Width = new GridLength(100).Value;
			BorderBrush = System.Windows.Media.Brushes.DarkGray;
			BorderThickness = new Thickness(1);
			Value = color;
		}

		public LinearColor Value
		{
			get { return _value; }
			set {
				_value = value;
				System.Windows.Media.Color c = 
					System.Windows.Media.Color.FromScRgb(value.A, value.R, value.G, value.B);
				Background = new SolidColorBrush(c);
			}
		}

		internal LinearColor _value;
	}


	// Actual value controls
	//
	// Those will combine label and one or more basic 
	// controls to fulfill a properties requirements

	internal abstract class ValueControl
	{
		internal string Label;
		internal UIElement Ctrl;
	}

	internal abstract class ValueControl<_ValueType> : ValueControl
	{
		internal ValueControl(_ValueType val) 
			: base()
		{
			_value = val;
		}

		internal virtual _ValueType Value
		{
			get { return (Ctrl as IValueContainer<_ValueType>).Value; }
			set { (Ctrl as IValueContainer<_ValueType>).Value = _value; }
		}
		internal _ValueType _value;
	}


	internal class SimpleValueControl<_ValueType> : ValueControl<_ValueType>
	{
		internal SimpleValueControl(string label, _ValueType val)
			: base(val)
		{
			Label = label;
			Ctrl = ControlFactory.Create(val);
		}
	}

	internal class ReadonlySimpleValueControl<_ValueType> : SimpleValueControl<_ValueType>
	{
		internal ReadonlySimpleValueControl(string label, _ValueType val)
			: base(label, val)
		{
			Ctrl.IsEnabled = false;
		}
	}


	internal class HexdumpControl : ValueControl<byte[]>
	{
		internal HexdumpControl(string label, byte[] val)
			: base(val)
		{
			Label = label;
			Ctrl = new TextBox() {
				Text = Helpers.Hexdump(val, indent:0),
				FontFamily = new FontFamily("Consolas, FixedSys, Terminal"),
				FontSize = 12,
			};
		}

	}


	internal class ImageControl : ValueControl<byte[]>
	{
		internal ImageControl(string label, byte[] val)
			: base(val)
		{
			Label = label;

			// Build image
			_image = ImageHandler.ImageFromBytes(val, depth:4);

			Grid grid = new Grid();
			grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
			grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(0, GridUnitType.Auto) });

			_label = new Label() {
				Content = string.Format(Translate._("ImageControl.Label"), _image.PixelWidth, _image.PixelHeight, 4),
				HorizontalAlignment = HorizontalAlignment.Left,
				VerticalAlignment = VerticalAlignment.Stretch,
				VerticalContentAlignment = VerticalAlignment.Center,
			};
			Grid.SetColumn(_label, 0);
			grid.Children.Add(_label);

			_button = new Button() {
				Content = Translate._("ImageControl.Button"),
				Width = 100,
				Height = 21,
			};
			_button.Click += _button_Click;
			Grid.SetColumn(_button, 1);
			grid.Children.Add(_button);

			Ctrl = grid;
		}

		private void _button_Click(object sender, RoutedEventArgs e)
		{
			new ImageDialog(null, Translate._("ImageDialog.Title"), _image).ShowDialog();
		}

		internal Label _label;
		internal Button _button;
		internal BitmapSource _image;

	}


	internal class VectorControl : ValueControl<object>
	{
		internal VectorControl(string label, Savegame.Properties.Vector val)
			: base(val)
		{
			Label = label;

			StackPanel panel = new StackPanel() {
				Orientation = Orientation.Horizontal,
			};

			panel.Children.Add(new FloatControl(val.X));
			panel.Children.Add(new FloatControl(val.Y));
			panel.Children.Add(new FloatControl(val.Z));

			Ctrl = panel;
		}
	}


	internal class ColorControl : ValueControl<Savegame.Properties.Color>
	{
		internal ColorControl(string label, Savegame.Properties.Color val)
			: base(val)
		{
			Label = label + " [RGBA]";

			StackPanel panel = new StackPanel() {
				Orientation = Orientation.Horizontal,
			};

			panel.Children.Add(new IntControl(val.R));
			panel.Children.Add(new IntControl(val.G));
			panel.Children.Add(new IntControl(val.B));
			panel.Children.Add(new IntControl(val.A));
			panel.Children.Add(new ColorDisplay(val));

			Ctrl = panel;
		}
	}

	internal class LinearColorControl : ValueControl<LinearColor>
	{
		internal LinearColorControl(string label, LinearColor val)
			: base(val)
		{
			Label = label + " [RGBA]";

			StackPanel panel = new StackPanel() {
				Orientation = Orientation.Horizontal,
			};

			panel.Children.Add(new FloatControl(val.R));
			panel.Children.Add(new FloatControl(val.G));
			panel.Children.Add(new FloatControl(val.B));
			panel.Children.Add(new FloatControl(val.A));
			panel.Children.Add(new LinearColorDisplay(val));

			Ctrl = panel;
		}
	}


	internal class QuatControl : ValueControl<object>
	{
		internal QuatControl(string label, Quat val)
			: base(val)
		{
			Label = label;

			StackPanel panel = new StackPanel() { Orientation = Orientation.Horizontal };
			panel.Children.Add(new FloatControl(val.A));
			panel.Children.Add(new FloatControl(val.B));
			panel.Children.Add(new FloatControl(val.C));
			panel.Children.Add(new FloatControl(val.D));

			Ctrl = panel;
		}
	}


	//internal class ObjectControl : SimpleValueControl<str>
	//{
	//	// Showing stuff swapped!
	//	internal ObjectControl(string label, ObjectProperty val)
	//		//: base(val.Name != null ? val.Name.ToString() : label, (str)val.Value)
	//		//: base(label, val.Name != null ? val.Name : DetailsPanel.strEMPTY)
	//		//: base(label, val.Value as str)
	//		: base(val.?, val.? as str)
	//	{ }
	//}
	//=> Must show one or more values


	internal class EnumControl : SimpleValueControl<str>
	{
		internal EnumControl(string label, EnumProperty val)
			: base(val.EnumName.ToString(), (str)val.Value)
		{ }
	}


	internal class NameControl : SimpleValueControl<str>
	{
		internal NameControl(string label, NameProperty val)
			: base(val.Name.ToString()/*label*/, (str)val.Value)
		{ }
	}


	internal class TextControl : SimpleValueControl<str>
	{
		internal TextControl(string label, TextProperty val)
			: base(val.Name.ToString()/*label*/, (str)val.Value)
		{ }
	}


	internal static class ControlFactory
	{
		internal static UIElement Create(object val)
		{
			// The 'bool' was just to get the right thing here, but it must be at least a byte,
			// and fields like 'int32:WasPlacedInLevel' use this control too.
			if (val is bool)	return new BoolControl ((bool)  val ? 1 : 0);
			if (val is byte)	return new ByteControl ((byte)  val);
			if (val is int)		return new IntControl  ((int)   val);
			if (val is long)	return new LongControl ((long)  val);
			if (val is float)	return new FloatControl((float) val);
			return				       new StrControl  ((str)   val);
		}

		internal static ValueControl CreateSimple(string label, object val, bool read_only = false)
		{
			if (!read_only)
			{
				if (val is bool)	return new SimpleValueControl<bool> (label, (bool)  val);
				if (val is byte)	return new SimpleValueControl<byte> (label, (byte)  val);
				if (val is int)		return new SimpleValueControl<int>  (label, (int)   val);
				if (val is long)	return new SimpleValueControl<long> (label, (long)  val);
				if (val is float)	return new SimpleValueControl<float>(label, (float) val);
				return                     new SimpleValueControl<str>  (label, (str)   val);
			}
			else
			{
				if (val is bool)	return new ReadonlySimpleValueControl<bool> (label, (bool)  val);
				if (val is byte)	return new ReadonlySimpleValueControl<byte> (label, (byte)  val);
				if (val is int)		return new ReadonlySimpleValueControl<int>  (label, (int)   val);
				if (val is long)	return new ReadonlySimpleValueControl<long> (label, (long)  val);
				if (val is float)	return new ReadonlySimpleValueControl<float>(label, (float) val);
				return                     new ReadonlySimpleValueControl<str>  (label, (str)   val);
			}
		}

		internal static ValueControl CreateValueProperty<_PropType>(string label, Property prop, object value = null)
			where _PropType : ValueProperty
		{
			_PropType value_prop = prop as _PropType;
			if (value == null)
				value = value_prop.Value;
			return CreateSimple(value_prop.Name.ToString(), value);
		}

		internal static ValueControl Create(string label, Property prop)
		{
			// BoolControl is getting an explicit 'bool' here to let 'Create(object val)' pick the right control
			if (prop is BoolProperty)	return CreateValueProperty<BoolProperty> (label, prop, ((byte)(prop as BoolProperty).Value) == 1);
			if (prop is ByteProperty)	return CreateValueProperty<ByteProperty> (label, prop);
			if (prop is IntProperty)	return CreateValueProperty<IntProperty>  (label, prop);
			if (prop is FloatProperty)	return CreateValueProperty<FloatProperty>(label, prop);
			if (prop is StrProperty)	return CreateValueProperty<StrProperty>  (label, prop);

			if (prop is P.Vector)		return new VectorControl     (label, prop as P.Vector);
			if (prop is Rotator)		return new VectorControl     (label, prop as P.Vector);
			if (prop is Scale)			return new VectorControl     (label, prop as P.Vector);
			if (prop is P.Color)		return new ColorControl      (label, prop as P.Color);
			if (prop is LinearColor)	return new LinearColorControl(label, prop as LinearColor);
			if (prop is Quat)			return new QuatControl       (label, prop as Quat);
		//	if (prop is ObjectProperty)	return new ObjectControl     (label, prop as ObjectProperty);
			if (prop is EnumProperty)	return new EnumControl       (label, prop as EnumProperty);
			if (prop is NameProperty)	return new NameControl       (label, prop as NameProperty);
			if (prop is TextProperty)	return new TextControl       (label, prop as TextProperty);
			return                             null;
		}

	}

}
