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

  public Point PartToWorld(int x, int y) { return new Point(x*Factor, y*Factor); }

  public void Render()
  { int x, x2, y, yd;
  
    { Point3 wtl = Misc.Unproject(new SPoint(App.Viewport[0], App.Viewport[1])),
             wbr = Misc.Unproject(new SPoint(App.Viewport[0]+App.Viewport[2], App.Viewport[1]+App.Viewport[3]));
      SPoint tl = WorldToPart(wtl.X, wtl.Y), br = WorldToPart(wbr.X, wbr.Y);
      x = tl.X; y = tl.Y; x2 = br.X; yd = br.Y-y+1;
    }

    /* TODO: these don't render correctly, but maybe i can just do away with them entirely
    */
    { Point wtl = PartToWorld(x, y), wbr = PartToWorld(x2+1, y+yd);
      wtl.X -= App.Camera.X; wbr.X -= App.Camera.X;
      wtl.Y -= App.Camera.Y; wbr.Y -= App.Camera.Y;

      GL.glDisable(GL.GL_LIGHTING);
      GL.glColor3d(.2, .2, .2);
      GL.glBegin(GL.GL_LINES);

      double c;
      int cd;
      cd = (x2-x+2); c = wtl.X;
      for(int t=0; t<cd; c+=Factor, t++)
      { GL.glVertex2d(c, wtl.Y);
        GL.glVertex2d(c, wbr.Y);
      }

      cd = yd+1; c = wtl.Y;
      for(int t=0; t<cd; c+=Factor, t++)
      { GL.glVertex2d(wtl.X, c);
        GL.glVertex2d(wbr.X, c);
      }
      GL.glEnd();
      GL.glEnable(GL.GL_LIGHTING);
    }

    for(; x<=x2; x++) for(int yi=0; yi<yd; yi++) RenderObjects(x, y+yi);
  }

  public void Update()
  { if(parts.Count>partArr.Length)
    { partArr = new DictionaryEntry[parts.Count];
      partArrChanged = true;
    }
    if(partArrChanged)
    { parts.CopyTo(partArr, 0);
      partArrChanged = false;
    }

    for(int pi=0,pcount=parts.Count; pi<pcount; pi++)
    { DictionaryEntry de = partArr[pi];
      ArrayList objs = (ArrayList)de.Value;
      if(objs.Count==0) unused.Add(de);
      else
      { SPoint part = (SPoint)de.Key;
        for(int i=objs.Count-1; i>=0; i--)
        { SpaceObject obj = (SpaceObject)objs[i];

          if(!obj.Is(ObjFlag.Dead)) obj.Update();

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
      partArrChanged = true;
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
    if(list==null)
    { parts[pt] = list = new ArrayList();
      partArrChanged = true;
    }
    return list;
  }

  void RenderObjects(int x, int y)
  { ArrayList objs = GetObjects(new SPoint(x, y));
    if(objs==null) return;
    foreach(SpaceObject obj in objs) obj.Render();
  }

  Hashtable parts = new Hashtable();
  
  static ArrayList unused = new ArrayList();
  static DictionaryEntry[] partArr = new DictionaryEntry[0];
  static bool partArrChanged;
}

} // namespace SpaceWinds