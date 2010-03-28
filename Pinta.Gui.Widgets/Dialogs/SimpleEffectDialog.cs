// 
// SimpleEffectDialog.cs
//  
// Author:
//       Jonathan Pobst <monkey@jpobst.com>
// 
// Copyright (c) 2010 Jonathan Pobst
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Reflection;
using System.Text;
using System.ComponentModel;

namespace Pinta.Gui.Widgets
{
	public class SimpleEffectDialog : Gtk.Dialog
	{
		const uint event_delay_millis = 100;
		uint event_delay_timeout_id;
		
		public SimpleEffectDialog (string title, Gdk.Pixbuf icon, object effectData)
		{
			Title = title;
			Icon = icon;
			EffectData = effectData;

			WidthRequest = 400;
			
			AddButton ("_Cancel", Gtk.ResponseType.Cancel);
			AddButton ("_OK", Gtk.ResponseType.Ok);
			DefaultResponse = Gtk.ResponseType.Ok;
			
			BuildDialog ();
			
			// This is just for padding, it should probably be done better
			AddWidget (new Gtk.Label ());
		}

		public object EffectData { get; private set; }

		public event PropertyChangedEventHandler EffectDataChanged;
		
		#region EffectData Parser
		private void BuildDialog ()
		{
			var members = EffectData.GetType ().GetMembers (BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

			foreach (var mi in members) {
				Type mType = GetTypeForMember (mi);

				if (mType == null)
					continue;
			
				string caption = null;
				string hint = null;
				bool skip = false;
				
				object[] attrs = mi.GetCustomAttributes (false);

				foreach (var attr in attrs) {
					if (attr is SkipAttribute)
						skip = true;
					else if (attr is CaptionAttribute)
						caption = ((CaptionAttribute)attr).Caption;
					else if (attr is HintAttribute)
						hint = ((HintAttribute)attr).Hint;
				}

				if (skip)
					continue;

				if (caption == null)
					caption = MakeCaption (mi.Name);

				if (mType == typeof (int))
					AddWidget (CreateSlider (caption, EffectData, mi, attrs));
				else if (mType == typeof (bool))
					AddWidget (CreateCheckBox (caption, EffectData, mi, attrs));
				else if (mType == typeof (Gdk.Point))
					AddWidget (CreatePointPicker (caption, EffectData, mi, attrs));
				else if (mType == typeof (Cairo.PointD))
					AddWidget (CreateOffsetPicker (caption, EffectData, mi, attrs));
				else if (mType == typeof (double) && (caption == "Angle" || caption == "Rotation"))
					AddWidget (CreateAnglePicker (caption, EffectData, mi, attrs));
				
				if (hint != null)
					AddWidget (CreateHintLabel (hint));
			}
		}

		private void AddWidget (Gtk.Widget widget)
		{
			widget.Show ();
			this.VBox.Add (widget);
		}
		#endregion

		#region Control Builders
		private HScaleSpinButtonWidget CreateSlider (string caption, object o, MemberInfo member, object[] attributes)
		{
			HScaleSpinButtonWidget widget = new HScaleSpinButtonWidget ();
			
			int min_value = -100;
			int max_value = 100;

			foreach (var attr in attributes) {
				if (attr is MinimumValueAttribute)
					min_value = ((MinimumValueAttribute)attr).Value;
				else if (attr is MaximumValueAttribute)
					max_value = ((MaximumValueAttribute)attr).Value;
			}
			
			widget.Label = caption;
			widget.MinimumValue = min_value;
			widget.MaximumValue = max_value;
			widget.DefaultValue = (int)GetValue (member, o);
			
			widget.ValueChanged += delegate (object sender, EventArgs e) {
				
				if (event_delay_timeout_id != 0)
					GLib.Source.Remove (event_delay_timeout_id);
				
				event_delay_timeout_id = GLib.Timeout.Add (event_delay_millis, () => {
					event_delay_timeout_id = 0;
					SetValue (member, o, widget.Value);
					return false;
				});
			};
			
			return widget;
		}
		
		private Gtk.CheckButton CreateCheckBox (string caption, object o, MemberInfo member, object[] attributes)
		{
			Gtk.CheckButton widget = new Gtk.CheckButton ();


			widget.Label = caption;
			widget.Active = (bool)GetValue (member, o);

			widget.Toggled += delegate (object sender, EventArgs e) {
				SetValue (member, o, widget.Active);
			};

			return widget;
		}

		private PointPickerWidget CreateOffsetPicker (string caption, object o, MemberInfo member, object[] attributes)
		{
			PointPickerWidget widget = new PointPickerWidget ();
						
			widget.Label = caption;
			widget.DefaultOffset = (Cairo.PointD)GetValue (member, o);

			widget.PointPicked += delegate (object sender, EventArgs e) {
				SetValue (member, o, widget.Offset);
			};

			return widget;
		}

		private PointPickerWidget CreatePointPicker (string caption, object o, MemberInfo member, object[] attributes)
		{
			PointPickerWidget widget = new PointPickerWidget ();
						
			widget.Label = caption;
			widget.DefaultPoint = (Gdk.Point)GetValue (member, o);

			widget.PointPicked += delegate (object sender, EventArgs e) {
				SetValue (member, o, widget.Point);
			};

			return widget;
		}

		private AnglePickerWidget CreateAnglePicker (string caption, object o, MemberInfo member, object[] attributes)
		{
			AnglePickerWidget widget = new AnglePickerWidget ();

			widget.Label = caption;
			widget.DefaultValue = (double)GetValue (member, o);
			
			widget.ValueChanged += delegate (object sender, EventArgs e) {				
				if (event_delay_timeout_id != 0)
					GLib.Source.Remove (event_delay_timeout_id);
				
				event_delay_timeout_id = GLib.Timeout.Add (event_delay_millis, () => {
					event_delay_timeout_id = 0;
					SetValue (member, o, widget.Value);
					return false;
				});
			};

			return widget;
		}
		
		private Gtk.Label CreateHintLabel (string hint)
		{
			Gtk.Label label = new Gtk.Label (hint);
			label.LineWrap = true;
			
			return label;
		}
		#endregion

		#region Static Reflection Methods
		private static object GetValue (MemberInfo mi, object o)
		{
			var fi = mi as FieldInfo;
			if (fi != null)
				return fi.GetValue (o);
			var pi = mi as PropertyInfo;

			var getMethod = pi.GetGetMethod ();
			return getMethod.Invoke (o, new object[0]);
		}

		private void SetValue (MemberInfo mi, object o, object val)
		{			
			var fi = mi as FieldInfo;
			var pi = mi as PropertyInfo;
			string fieldName = null;
			
			if (fi != null) {
				fi.SetValue (o, val);
				fieldName = fi.Name;
			} else if (pi != null) {
				var setMethod = pi.GetSetMethod ();
				setMethod.Invoke (o, new object[] { val });
				fieldName = pi.Name;
			}
						
			if (EffectDataChanged != null)
				EffectDataChanged (this, new PropertyChangedEventArgs(fieldName));
		}

		// Returns the type for fields and properties and null for everything else
		private static Type GetTypeForMember (MemberInfo mi)
		{
			if (mi is FieldInfo)
				return ((FieldInfo)mi).FieldType;
			else if (mi is PropertyInfo)
				return ((PropertyInfo)mi).PropertyType;
				
			return null;
		}

		private static string MakeCaption (string name)
		{
			var sb = new StringBuilder (name.Length);
			bool nextUp = true;

			foreach (char c in name) {
				if (nextUp) {
					sb.Append (Char.ToUpper (c));
					nextUp = false;
				} else {
					if (c == '_') {
						sb.Append (' ');
						nextUp = true;
						continue;
					}
					if (Char.IsUpper (c))
						sb.Append (' ');
					sb.Append (c);
				}
			}
			
			return sb.ToString ();
		}
		#endregion
	}
}
