using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using GameLib.Mathematics;
using GameLib.Mathematics.ThreeD;
using GameLib.Interop.OpenGL;
using Point2=GameLib.Mathematics.TwoD.Point;
using RectF=System.Drawing.RectangleF;

namespace SpaceWinds
{

#region Model
public abstract class Model
{ public Model(XmlElement data) { Data=data; MinX=MinY=MinZ=float.MaxValue; MaxX=MaxY=MaxZ=float.MinValue; }
  public abstract void Render();
  public XmlElement Data;
  public float RadiusSquared, MinX, MinY, MinZ, MaxX, MaxY, MaxZ;

  public static Model Load(string modelName) { return Load(modelName, null); }
  public static Model Load(string modelName, XmlElement data)
  { Model model;
    if(!models.TryGetValue(modelName, out model))
    { if(data==null)
      { XmlDocument doc = Misc.LoadXml(modelName+".xml", false);
        if(doc!=null)
        { data = doc.DocumentElement;
          if(data.LocalName=="sphere") model = new IcoSphereModel(modelName, data);
        }
      }
      if(model==null) model = new ObjModel(modelName, data);
      models[modelName] = model;
    }
    return model;
  }

  static Dictionary<string,Model> models = new Dictionary<string,Model>();
}
#endregion

#region ObjModel
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
  { public SubObject(TextReader tr, ref string nameLine, ArrayList points, ArrayList norms, ArrayList texCoords)
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
        else if(line.StartsWith("usemtl "))
        { string mat = line.Substring(7);
          if(mat!="(null)")
          { Material = Material.Get(mat);
            if(Material==null) throw new ArgumentException("Material not found: "+mat);
          }
        }
      }
      
      faces = (int[])fcs.ToArray(typeof(int));
    }

    public void Render(ObjModel model)
    { if(!smoothShading) GL.glShadeModel(GL.GL_FLAT);

      Point[]   points = model.points;
      Vector[] normals = model.normals;
      Material.Current = Material;
      GL.glBegin(GL.GL_TRIANGLES);
        if(Material==null || !Material.UsesTexture)
          for(int v=0; v<faces.Length; v+=3)
          { int i = faces[v+2];
            if(i!=-1) GL.glNormal3d(normals[i]);
            GL.glVertex3d(points[faces[v]]);
          }
        else
        { Point2[] texCoords = model.texCoords;
          for(int v=0; v<faces.Length; v+=3)
          { int i = faces[v+2];
            if(i!=-1) GL.glNormal3d(normals[i]);
            i = faces[v+1];
            if(i!=-1) GL.glTexCoord2d(texCoords[i]);
            GL.glVertex3d(points[faces[v]]);
          }
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
  { ArrayList subs=new ArrayList(), pts=new ArrayList(), norms=new ArrayList(), mounts=new ArrayList(),
              tex=new ArrayList();
    string line = tr.ReadLine();
    while(true)
    { if(line==null) break;
      if(line.StartsWith("o "))
      { SubObject so = new SubObject(tr, ref line, pts, norms, tex);
        if(so.Name.StartsWith("exhaust_port_")) { }
        else if(so.Name.StartsWith("mount_")) // TODO: fix this mount loading code
        { Match m = mountre.Match(so.Name);
          XmlNode node = Data.SelectSingleNode("mount[@id='"+m.Groups[1].Value+"']");

          MountClass mc = new MountClass();
          mc.Model = Model.Load(Xml.Attr(node, "model"));
          mc.CenterAngle = (float)(Xml.Float(node, "center") * MathConst.DegreesToRadians);
          double n = Xml.Float(node, "range", 360);
          mc.MaxTurn = (float)(n==180 ? Math.PI/2 : n>=360 ? Math.PI : n*(MathConst.DegreesToRadians*0.5));
          mc.TurnSpeed = (float)(Xml.Float(node, "speed", 1080) * MathConst.DegreesToRadians);

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
    texCoords  = tex.Count==0 ? null : (Point2[])tex.ToArray(typeof(Point2));
    subObjects = (SubObject[])subs.ToArray(typeof(SubObject));
    Mounts     = (MountClass[])mounts.ToArray(typeof(MountClass));

    for(int i=0; i<points.Length; i++)
    { Point pt = points[i];
      MinX = Math.Min(MinX, (float)pt.X);
      MaxX = Math.Max(MaxX, (float)pt.X);
      MinY = Math.Min(MinY, (float)pt.Y);
      MaxY = Math.Max(MaxY, (float)pt.Y);
      MinZ = Math.Min(MinZ, (float)pt.Z);
      MaxZ = Math.Max(MaxZ, (float)pt.Z);
    }

    float xd=Math.Max(Math.Abs(MinX), Math.Abs(MaxX)), yd=Math.Max(Math.Abs(MinY), Math.Abs(MaxY));
    RadiusSquared = xd*xd+yd*yd;
  }

  SubObject[] subObjects;
  Point[] points;
  Vector[] normals;
  Point2[] texCoords;
  
  static Regex mountre = new Regex(@"^mount_(\d+)", RegexOptions.Singleline);
}
#endregion

#region SphereModel
public abstract class SphereModel : Model
{ public SphereModel(string modelName, XmlElement data) : base(data)
  { Radius = Xml.Float(data, "radius");
    MinX = MinY = MinZ = -Radius;
    MaxX = MaxY = MaxZ = Radius;
    RadiusSquared = Radius * Radius;

    string str = Xml.Attr(data, "mtllib");
    if(!Xml.IsEmpty(str)) ObjMaterial.LoadLibrary(str);

    str = Xml.Attr(data, "material");
    if(!Xml.IsEmpty(str))
    { Material = Material.Get(str);
      if(Material==null) throw new ArgumentException("Material not found: "+str);
    }
    else Material = Material.Get(modelName);
    
    poleHack = Xml.IsTrue(data, "poleHack");
  }
  
  public override void Render()
  { Material.Current = Material;

    GL.glBegin(GL.GL_TRIANGLES);
      if(Material==null || !Material.UsesTexture)
        for(int v=0; v<faces.Length; v++)
        { int i = faces[v];
          GL.glNormal3d(normals[i]);
          GL.glVertex3d(points[i]);
        }
      else
        for(int v=0; v<faces.Length; v+=3)
        { int a=faces[v], b=faces[v+1], c=faces[v+2];
          Point2 ta=texCoords[a], tb=texCoords[b], tc=texCoords[c];
          
          // detect and remove the seam
          if(Math.Abs(ta.X-tb.X)>=0.5 || Math.Abs(tb.X-tc.X)>=0.5 || Math.Abs(tc.X-ta.X)>=0.5)
          { if(ta.X<0.5) ta.X += 1;
            if(tb.X<0.5) tb.X += 1;
            if(tc.X<0.5) tc.X += 1;
          }

          // mitigate the distortion at the poles
          if(poleHack)
          { if(ta.Y==0 || ta.Y==1) ta.X = TexMidpoint(tb.X, tc.X);
            else if(tb.Y==0 || tb.Y==1) tb.X = TexMidpoint(ta.X, tc.X);
            else if(tc.Y==0 || tc.Y==1) tc.X = TexMidpoint(ta.X, tb.X);
          }

          GL.glNormal3d(normals[a]);
          GL.glTexCoord2d(ta);
          GL.glVertex3d(points[a]);

          GL.glNormal3d(normals[b]);
          GL.glTexCoord2d(tb);
          GL.glVertex3d(points[b]);

          GL.glNormal3d(normals[c]);
          GL.glTexCoord2d(tc);
          GL.glVertex3d(points[c]);
        }
    GL.glEnd();
  }

  public readonly Material Material;
  public readonly float Radius;

  protected void CreateSphericalTexCoords()
  { for(int i=0; i<normals.Length; i++)
    { Vector normal = normals[i];
      double phi = Math.Acos(normal.Z), U;
      if(normal.Z==1 || normal.Z==-1) U = 0.5;
      else
      { U = Math.Acos(Math.Max(Math.Min(normal.Y/Math.Sin(phi), 1), -1)) / (Math.PI*2);
        if(normal.X<=0) U = 1-U;
      }
      texCoords[i] = new Point2(U, phi/Math.PI);
    }
  }

  protected Point[] points;
  protected Vector[] normals;
  protected Point2[] texCoords;
  protected int[] faces;
  protected bool poleHack;

  static double TexMidpoint(double a, double b)
  { double diff = b-a;
    if(Math.Abs(diff)>=0.5)
    { if(a<0.5) a += 1;
      if(b<0.5) b += 1;
      diff = a-b;
    }
    return a+diff/2;
  }
}
#endregion

#region IcoSphereModel
public sealed class IcoSphereModel : SphereModel
{ public IcoSphereModel(string modelName, XmlElement data) : base(modelName, data)
  { Make(Radius<=App.SizeAtNear ? 1 : 2);
  }

  int AddPoint(int a, int b, Hashtable hash, ref int index)
  { if(a>b) { int t=a; a=b; b=t; }
    int pi = a*points.Length + b;
    object ret = hash[pi];
    if(ret!=null) return (int)ret;
    points[index] = Midpoint(a, b);
    hash[pi] = index;
    return index++;
  }

  void Make(int subdivisions)
  { int npoints=12, nedges=30;
    for(int i=0; i<subdivisions; i++) { npoints += nedges; nedges *= 4; }

    points    = new Point[npoints];
    normals   = new Vector[npoints];
    texCoords = Material!=null && Material.UsesTexture ? new Point2[npoints] : null;
    Hashtable edgeHash = new Hashtable(nedges/4); // keep track of which edges we've split already
    npoints   = 12;

    double one = (Math.Sqrt(5)+1)*0.5, tau = one/Math.Sqrt(Math.Pow(one, 2)+1);
    one = 1 / Math.Sqrt(Math.Pow(one, 2)+1);

    points[ 0] = new Point(-one, 0, tau);
    points[ 1] = new Point(one, 0, tau);
    points[ 2] = new Point(-one, 0, -tau);
    points[ 3] = new Point(one, 0, -tau);
    points[ 4] = new Point(0, tau, one);
    points[ 5] = new Point(0, tau, -one);
    points[ 6] = new Point(0, -tau, one);
    points[ 7] = new Point(0, -tau, -one);
    points[ 8] = new Point(tau, one, 0);
    points[ 9] = new Point(-tau, one, 0);
    points[10] = new Point(tau, -one, 0);
    points[11] = new Point(-tau, -one, 0);

    faces = new int[20*3]
    { 0,4,1,   0,9,4,   9,5,4,   4,5,8,   4,8,1,     
      8,10,1,  8,3,10,  5,3,8,   5,2,3,   2,7,3,     
      7,10,3,  7,6,10,  7,11,6,  11,0,6,  0,1,6,  
      6,1,10,  9,0,11,  9,11,2,  9,2,5,   7,2,11
    };

    while(subdivisions-->0)
    { int[] newfaces = new int[faces.Length*4];
      edgeHash.Clear();

      for(int i=0,j=0; i<faces.Length; i+=3) // subdivide each face
      { int a=faces[i], b=faces[i+1], c=faces[i+2];
        int na=AddPoint(a, c, edgeHash, ref npoints), nb=AddPoint(a, b, edgeHash, ref npoints),
            nc=AddPoint(b, c, edgeHash, ref npoints);

        newfaces[j++] = a; // create the new faces
        newfaces[j++] = nb;
        newfaces[j++] = na;

        newfaces[j++] = nb;
        newfaces[j++] = b;
        newfaces[j++] = nc;

        newfaces[j++] = na;
        newfaces[j++] = nb;
        newfaces[j++] = nc;

        newfaces[j++] = na;
        newfaces[j++] = nc;
        newfaces[j++] = c;
      }

      faces = newfaces;
    }
    
    for(int i=0; i<points.Length; i++)
    { normals[i] = new Vector(points[i]);
      points[i].X *= Radius;
      points[i].Y *= Radius;
      points[i].Z *= Radius;
    }

    if(texCoords!=null) CreateSphericalTexCoords();
  }

  Point Midpoint(int a, int b)
  { Point pa = points[a];
    return new Vector(pa+(points[b]-pa)*0.5).Normal.ToPoint();
  }
}
#endregion

} // namespace SpaceWinds