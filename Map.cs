using System;
using System.Collections;
using GameLib.Interop.OpenGL;
using GameLib.Mathematics.TwoD;
using SPoint=System.Drawing.Point;
using Point3=GameLib.Mathematics.ThreeD.Point;

namespace SpaceWinds
{

public sealed class Map
{ public const double Factor = 100; // 100 world units in a grid unit

  public readonly string Name;

  public void Add(SpaceObject obj)
  { MakeObjects(WorldToPart(obj.Pos)).Add(obj);
    obj.Map = this;
  }

  public void Remove(SpaceObject obj)
  { GetObjects(obj.Pos).Remove(obj);
    obj.Map = null;
  }

  public ArrayList GetObjects(Point pt) { return (ArrayList)parts[WorldToPart(pt)]; }
  public ArrayList GetObjects(SPoint pt) { return (ArrayList)parts[pt]; }

  public void Render(Point3 camera)
  { double x, y, x2, y2, z;
    // FIXME: this code isn't correct
    GLU.gluProject(-App.XYSize, -App.XYSize, App.FarZ, App.ModelMatrix, App.ProjectionMatrix, App.Viewport,
                   out x, out y, out z);
    GLU.gluProject(App.XYSize, App.XYSize, App.FarZ, App.ModelMatrix, App.ProjectionMatrix, App.Viewport,
                   out x2, out y2, out z);
    x = (x2-x)*0.5; // get the size of the visible area, in world coordinates, divided by 2
    y = (y-y2)*0.5;
    
    x = y = 0; // FIXME: remove this after fixing above code

    GL.glPushMatrix();
      GL.glTranslated(-camera.X, -camera.Y, -camera.Z);
      SPoint topLeft = WorldToPart(camera.X-x, camera.Y-y), topRight = WorldToPart(camera.X+x, camera.Y-y),
             btmLeft = WorldToPart(camera.X-x, camera.Y+y);
      RenderObjects(topLeft);
      if(topRight!=topLeft) RenderObjects(topRight);
      if(topLeft!=btmLeft)
      { RenderObjects(btmLeft);
        if(topRight!=topLeft) RenderObjects(WorldToPart(camera.X+x, camera.Y+y));
      }
    GL.glPopMatrix();
  }

  public void Update()
  { if(parts.Count>partArr.Length) partArr = new DictionaryEntry[parts.Count];
    parts.CopyTo(partArr, 0);

    foreach(DictionaryEntry de in partArr)
    { ArrayList objs = (ArrayList)de.Value;
      if(objs.Count==0) unused.Add(de);
      else
      { SPoint part = (SPoint)de.Key;
        for(int i=objs.Count-1; i>=0; i--)
        { SpaceObject obj = (SpaceObject)objs[i];

          if(!obj.Is(ObjFlag.Dead))
          { int count = objs.Count;
            obj.Update();
            i += count-objs.Count; // TODO: make this robust (so nothing gets missed and nothing gets updated twice)
          }

          if(obj.Is(ObjFlag.Dead))
          { objs.RemoveAt(i);
            obj.Map = null;
          }
          else
          { SPoint npart = WorldToPart(obj.Pos);
            if(part!=npart)
            { objs.RemoveAt(i);
              MakeObjects(npart).Add(obj);
            }
          }
        }
      }
    }
    
    if(unused.Count!=0)
    { foreach(DictionaryEntry de in unused) if(((ArrayList)de.Value).Count==0) parts.Remove(de.Key);
      unused.Clear();
    }
  }

  public Point WorldToCoord(Point pt)
  { return new Point(Math.Round(pt.X/Factor, 2), Math.Round(pt.Y/Factor, 2)); // note that this assumes Factor is a power of 10
  }

  public SPoint WorldToPart(Point pt) { return WorldToPart(pt.X, pt.Y); }
  public SPoint WorldToPart(double x, double y)
  { return new SPoint((int)Math.Floor(x/Factor), (int)Math.Floor(y/Factor));
  }

  ArrayList MakeObjects(SPoint pt)
  { ArrayList list = (ArrayList)parts[pt];
    if(list==null) parts[pt] = list = new ArrayList();
    return list;
  }

  void RenderObjects(SPoint pt)
  { ArrayList objs = GetObjects(pt);
    if(objs==null) return;
    foreach(SpaceObject obj in objs) obj.Render();
  }

  Hashtable parts = new Hashtable();
  
  static ArrayList unused = new ArrayList();
  static DictionaryEntry[] partArr = new DictionaryEntry[0];
}

} // namespace SpaceWinds