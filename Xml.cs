using System;
using System.Text.RegularExpressions;
using System.Xml;

namespace SpaceWinds
{

public class Xml
{ Xml() { }

  public static string Attr(XmlNode node, string attr) { return Attr(node, attr, null); }
  public static string Attr(XmlNode node, string attr, string defaultValue)
  { if(node==null) return defaultValue;
    XmlAttribute an = node.Attributes[attr];
    return an==null ? defaultValue : an.Value;
  }

  public static XmlAttribute AttrNode(XmlNode node, string attr) { return node==null ? null : node.Attributes[attr]; }

  public static float Float(XmlAttribute attr) { return Float(attr, 0); }
  public static float Float(XmlAttribute attr, float defaultValue)
  { return attr==null ? defaultValue : float.Parse(attr.Value);
  }
  public static float Float(XmlNode node, string attr) { return Float(node.Attributes[attr], 0); }
  public static float Float(XmlNode node, string attr, float defaultValue)
  { return Float(node.Attributes[attr], defaultValue);
  }

  public static int Int(XmlAttribute attr) { return Int(attr, 0); }
  public static int Int(XmlAttribute attr, int defaultValue)
  { return attr==null ? defaultValue : int.Parse(attr.Value);
  }
  public static int Int(XmlNode node, string attr) { return Int(node.Attributes[attr], 0); }
  public static int Int(XmlNode node, string attr, int defaultValue)
  { return Int(node.Attributes[attr], defaultValue);
  }

  public static bool IsEmpty(XmlAttribute attr) { return attr==null || IsEmpty(attr.Value); }
  public static bool IsEmpty(string str) { return str==null || str==""; }
  public static bool IsEmpty(XmlNode node, string attr) { return IsEmpty(node.Attributes[attr]); }

  public static bool IsTrue(XmlAttribute attr) { return attr!=null && IsTrue(attr.Value); }
  public static bool IsTrue(string str) { return str!=null && str!="" && str!="0" && str.ToLower()!="false"; }
  public static bool IsTrue(XmlNode node, string attr) { return IsTrue(node.Attributes[attr]); }

  public static string[] List(XmlNode node, string attr) { return List(node.Attributes[attr]); }
  public static string[] List(XmlAttribute attr) { return IsEmpty(attr) ? new string[0] : split.Split(attr.Value); }
  public static string[] List(string data) { return IsEmpty(data) ? new string[0] : split.Split(data); }

  static Regex ltbl   = new Regex(@"^(?:\s*\n)+|\s+$", RegexOptions.Singleline);
  static Regex lspc   = new Regex(@"^\s+", RegexOptions.Singleline);
  static Regex split  = new Regex(@"\s+", RegexOptions.Singleline);
}

} // namespace SpaceWinds