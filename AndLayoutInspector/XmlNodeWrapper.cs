﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Xml;

namespace AndLayoutInspector
{
    [TypeConverter(typeof(XmlNodeWrapperConverter))]
    class XmlNodeWrapper
    {
        private readonly XmlNode node;

        public XmlNodeWrapper(XmlNode node)
        {
            this.node = node;
        }

        class XmlNodeWrapperConverter : ExpandableObjectConverter
        {

            public readonly bool showChildern = false;

            public override PropertyDescriptorCollection GetProperties(ITypeDescriptorContext context, object value, Attribute[] attributes)
            {
                List<PropertyDescriptor> props = new List<PropertyDescriptor>();
                XmlElement el = ((XmlNodeWrapper)value).node as XmlElement;
                if (el != null)
                {
                    foreach (XmlAttribute attr in el.Attributes)
                    {
                        props.Add(new XmlNodeWrapperPropertyDescriptor(attr));
                    }
                }
                if (showChildern)
                {
                    foreach (XmlNode child in ((XmlNodeWrapper)value).node.ChildNodes)
                    {
                        props.Add(new XmlNodeWrapperPropertyDescriptor(child));
                    }
                }
                return new PropertyDescriptorCollection(props.ToArray(), true);
            }

            public override object ConvertTo(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value, Type destinationType)
            {
                return destinationType == typeof(string)
                    ? ((XmlNodeWrapper)value).node.InnerXml
                    : base.ConvertTo(context, culture, value, destinationType);
            }
        }

        class XmlNodeWrapperPropertyDescriptor : PropertyDescriptor
        {
            private static readonly Attribute[] nix = Array.Empty<Attribute>();
            private readonly XmlNode node;
            public XmlNodeWrapperPropertyDescriptor(XmlNode node) : base(GetName(node), nix)
            {
                this.node = node;
            }

            static string GetName(XmlNode node)
            {
                switch (node.NodeType)
                {
                    case XmlNodeType.Attribute: return "@" + node.Name;
                    case XmlNodeType.Element: return node.Name;
                    case XmlNodeType.Comment: return "<!-- -->";
                    case XmlNodeType.Text: return "(text)";
                    default: return node.NodeType + ":" + node.Name;
                }
            }

            public override bool ShouldSerializeValue(object component)
            {
                return false;
            }

            public override void SetValue(object component, object value)
            {
                node.Value = (string)value;
            }

            public override bool CanResetValue(object component)
            {
                return !IsReadOnly;
            }

            public override void ResetValue(object component)
            {
                SetValue(component, "");
            }

            public override Type PropertyType
            {
                get
                {
                    switch (node.NodeType)
                    {
                        case XmlNodeType.Element:
                            return typeof(XmlNodeWrapper);
                        default:
                            return typeof(string);
                    }
                }
            }

            public override bool IsReadOnly
            {
                get
                {
                    switch (node.NodeType)
                    {
                        case XmlNodeType.Attribute:
                        case XmlNodeType.Text:
                            return false;
                        default:
                            return true;
                    }
                }
            }

            public override object GetValue(object component)
            {
                switch (node.NodeType)
                {
                    case XmlNodeType.Element:
                        return new XmlNodeWrapper(node);
                    default:
                        return node.Value;
                }
            }

            public override Type ComponentType
            {
                get => typeof(XmlNodeWrapper);
            }
        }
    }
}
