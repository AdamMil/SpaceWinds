using System;
using System.Collections;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using GameLib.Mathematics;
using GameLib.Mathematics.ThreeD;
using GameLib.Interop.OpenGL;

namespace SpaceWinds
{

public abstract class Model
{ public Model(XmlElement data) { Data=data; }
  public abstract void Render();
  public XmlElement Data;
  
  public static Model Load(string modelName) { return Load(modelName, null); }
  public static Model Load(string modelName, XmlElement data)
  { Model model = (Model)models[modelName];
    if(model==null)
    { if(data==null)
      { XmlDocument doc = Misc.LoadXml(modelName+".xml", false);
        if(doc!=null) data = doc.DocumentElement;
      }
      models[modelName] = model = new ObjModel(modelName, data);
    }
    return model;
  }

  static Hashtable models = new Hashtable();
}

public sealed class ObjModel : Model
{ public ObjModel(string modelName, XmlElement data) : base(data)
  { modelName += ".obj";
    if(data!=null) modelName = Xml.Attr(data, "model", modelName);
    TextReader tr = new StreamReader(App.DataPath+modelName);
    Load(tr);
    tr.Close();
  }

  public MountClass[] Mounts;

  public override void Render() { for(int i=0; i<subObjects.Length; i++) subObjects[i].Render(this); }

  struct SubObject
  { public SubObject(TextReader tr, ref string nameLine, ArrayList points, ArrayList norms)
    { Name = nameLine.Substring(2);
      Material = null;
      smoothShading = false;

      ArrayList fcs = new ArrayList();
      int[] face = new int[12];

      while(true)
      { string line = tr.ReadLine();
        if(line==null || line.StartsWith("o ")) { nameLine = line; break; }
        
        if(line.StartsWith("v "))
        { string[] s = line.Split(' ');
          points.Add(new Point(double.Parse(s[1]), -double.Parse(s[2]), double.Parse(s[3])));
        }
        else if(line.StartsWith("vn "))
        { string[] s = line.Split(' ');
          norms.Add(new Vector(double.Parse(s[1]), -double.Parse(s[2]), double.Parse(s[3])));
        }
        else if(line.StartsWith("f "))
        { string[] s = line.Split(' ');

          for(int i=1,j=0; i<s.Length; i++)
            foreach(string d in s[i].Split('/')) face[j++] = (d=="" ? 0 : int.Parse(d)) - 1;

          if(s.Length==4) for(int i=0; i<9; i++) fcs.Add(face[i]);
          else
          { for(int i=0; i<6; i++) fcs.Add(face[i]); // 1, 2, 4
            for(int i=9; i<12; i++) fcs.Add(face[i]);
            for(int i=3; i<12; i++) fcs.Add(face[i]); // 2, 3, 4
          }
        }
        else if(line.StartsWith("s ")) smoothShading = line=="s 1";
        else if(line.StartsWith("usemtl ")) Material = Material.Get(line.Substring(7));
      }
      
      faces = (int[])fcs.ToArray(typeof(int));
    }

    public void Render(ObjModel model)
    { if(!smoothShading) GL.glShadeModel(GL.GL_FLAT);

      Point[]   points = model.points;
      Vector[] normals = model.normals;
      if(Material!=null) Material.Current = Material;
      GL.glBegin(GL.GL_TRIANGLES);
        for(int v=0; v<faces.Length; v+=3)
        { int i = faces[v+2];
          if(i!=-1) GL.glNormal3d(normals[i]);
          GL.glVertex3d(points[faces[v]]);
        }
      GL.glEnd();

      if(!smoothShading) GL.glShadeModel(GL.GL_SMOOTH);
    }

    public readonly string Name;
    public readonly Material Material;

    internal readonly int[] faces;
    readonly bool smoothShading;
  }

  void Load(TextReader tr)
  { ArrayList subs=new ArrayList(), pts=new ArrayList(), norms=new ArrayList(), mounts=new ArrayList();
    string line = tr.ReadLine();
    while(true)
    { if(line==null) break;
      if(line.StartsWith("o "))
      { SubObject so = new SubObject(tr, ref line, pts, norms);
        if(so.Name.StartsWith("exhaust_port_")) { }
        else if(so.Name.StartsWith("mount_")) // TODO: fix this mount loading code
        { Match m = mountre.Match(so.Name);
          XmlNode node = Data.SelectSingleNode("mount[@id='"+m.Groups[1].Value+"']");

          MountClass mc = new MountClass();
          mc.Model = Model.Load(Xml.Attr(node, "model"));
          mc.CenterAngle = Xml.Float(node, "center") * MathConst.DegreesToRadians;
          double n = Xml.Float(node, "range", 360);
          mc.MaxTurn = n==180 ? Math.PI/2 : n>=360 ? Math.PI : n*(MathConst.DegreesToRadians*0.5);
          mc.TurnSpeed = Xml.Float(node, "speed", 1080) * MathConst.DegreesToRadians;

          for(int i=0; i<so.faces.Length; i+=3)
          { Point pt = (Point)pts[so.faces[i]];
            mc.RenderOffset.X += pt.X;
            mc.RenderOffset.Y += pt.Y;
            mc.RenderOffset.Z += pt.Z;
          }
          mc.RenderOffset /= so.faces.Length/3;

          mounts.Add(mc);
        }
        else subs.Add(so);
      }
      else
      { if(line.StartsWith("mtllib ")) ObjMaterial.LoadLibrary(line.Substring(line.IndexOf(' ')+1));
        line = tr.ReadLine();
      }
    }

    points     = (Point[])pts.ToArray(typeof(Point));
    normals    = (Vector[])norms.ToArray(typeof(Vector));
    subObjects = (SubObject[])subs.ToArray(typeof(SubObject));
    Mounts     = (MountClass[])mounts.ToArray(typeof(MountClass));
  }

  SubObject[] subObjects;
  Point[] points;
  Vector[] normals;
  
  static Regex mountre = new Regex(@"^mount_(\d+)", RegexOptions.Singleline);
}

} // namespace SpaceWinds