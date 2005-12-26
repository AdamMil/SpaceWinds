using System;
using System.Collections;
using System.IO;
using System.Text.RegularExpressions;
using GameLib.Mathematics;
using GameLib.Mathematics.ThreeD;
using GameLib.Interop.OpenGL;

namespace SpaceWinds
{

public abstract class Model
{ public abstract void Render();
}

public sealed class CubeModel : Model
{
  public override void Render()
  { GL.glShadeModel(GL.GL_FLAT);
    GL.glBegin(GL.GL_QUADS);
      // front
      GL.glNormal3d(0, 0, 1);
      GL.glColor3d(1, 0, 0);
      GL.glVertex3d(-2, -2, 2);
      GL.glVertex3d(2, -2, 2);
      GL.glVertex3d(2, 2, 2);
      GL.glVertex3d(-2, 2, 2);

      // back
      GL.glNormal3d(0, 0, -1);
      GL.glColor3d(0, 1, 1);
      GL.glVertex3d(-2, -2, -2);
      GL.glVertex3d(2, -2, -2);
      GL.glVertex3d(2, 2, -2);
      GL.glVertex3d(-2, 2, -2);
      
      // left
      GL.glNormal3d(-1, 0, 0);
      GL.glColor3d(0, 1, 0);
      GL.glVertex3d(-2, -2, 2);
      GL.glVertex3d(-2, 2, 2);
      GL.glVertex3d(-2, 2, -2);
      GL.glVertex3d(-2, -2, -2);

      // right
      GL.glNormal3d(1, 0, 0);
      GL.glColor3d(1, 1, 0);
      GL.glVertex3d(2, -2, 2);
      GL.glVertex3d(2, 2, 2);
      GL.glVertex3d(2, 2, -2);
      GL.glVertex3d(2, -2, -2);
      
      // top
      GL.glNormal3d(0, -1, 0);
      GL.glColor3d(0, 0, 1);
      GL.glVertex3d(-2, -2, -2);
      GL.glVertex3d(2, -2, -2);
      GL.glVertex3d(2, -2, 2);
      GL.glVertex3d(-2, -2, 2);

      // bottom
      GL.glNormal3d(0, 1, 0);
      GL.glColor3d(1, 0, 1);
      GL.glVertex3d(-2, 2, -2);
      GL.glVertex3d(2, 2, -2);
      GL.glVertex3d(2, 2, 2);
      GL.glVertex3d(-2, 2, 2);
    GL.glEnd();
  }
}

public sealed class ObjModel : Model
{ public ObjModel(string modelName)
  { TextReader tr = new StreamReader(App.DataPath+modelName+".obj");
    Load(tr);
    tr.Close();
  }

  public Mount[] Mounts;

  public override void Render() { for(int i=0; i<subObjects.Length; i++) { GL.glColor4d((i*20+80)/255.0, (i*20+80)/255.0, (i*20+80)/255.0, 1); subObjects[i].Render(this); } }

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
          Mount mount = new Mount();
          mount.Class = new MountClass();

          mount.Class.Model = new ObjModel(m.Groups[1].Value);
          mount.Class.CenterAngle = int.Parse(m.Groups[2].Value) * MathConst.DegreesToRadians;

          int n;
          n = int.Parse(m.Groups[3].Value);
          mount.Class.MaxTurn = n==90 ? Math.PI/2 : n>=180 ? Math.PI : n*MathConst.DegreesToRadians;

          mount.Class.TurnSpeed = Math.PI*6;

          for(int i=0; i<so.faces.Length; i+=3)
          { Point pt = (Point)pts[so.faces[i]];
            mount.Class.RenderOffset.X += pt.X;
            mount.Class.RenderOffset.Y += pt.Y;
            mount.Class.RenderOffset.Z += pt.Z;
          }
          mount.Class.RenderOffset /= so.faces.Length/3;
          
          mounts.Add(mount);
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
    Mounts     = (Mount[])mounts.ToArray(typeof(Mount));
  }

  SubObject[] subObjects;
  Point[] points;
  Vector[] normals;
  
  static Regex mountre = new Regex(@"^mount_([a-zA-Z0-9]+)_(\d+)_(\d+)", RegexOptions.Singleline);
}

} // namespace SpaceWinds