using System;
using System.Collections.Generic;
using GameLib.Interop.OpenGL;
using GameLib.Mathematics.TwoD;
using SPoint=System.Drawing.Point;
using Point3=GameLib.Mathematics.ThreeD.Point;

namespace SpaceWinds
{

public sealed class Map
{ public const float Factor = 100; // 100 world units in a grid unit

  public readonly string Name;

  public void Add(SpaceObject obj)
  { MakeObjects(WorldToPart(obj.X, obj.Y)).Add(obj);
    obj.Map = this;
  }

  public void Remove(SpaceObject obj)
  { GetObjects(obj.X, obj.Y).Remove(obj);
    obj.Map = null;
  }

  public List<SpaceObject> GetObjects(float x, float y) { return GetObjects(WorldToPart(x, y)); }
  public List<SpaceObject> GetObjects(SPoint pt)
  { List<SpaceObject> list;
    parts.TryGetValue(pt, out list);
    return list;
  }

  public Point PartToWorld(int x, int y) { return new Point(x*Factor, y*Factor); }

  Point3[] stars;

  public void Render()
  { int x, x2, y, yd;
  
    { Point3 wtl = Misc.Unproject(new SPoint(App.Viewport[0], App.Viewport[1])),
             wbr = Misc.Unproject(new SPoint(App.Viewport[0]+App.Viewport[2], App.Viewport[1]+App.Viewport[3]));
      SPoint tl = WorldToPart((float)wtl.X, (float)wtl.Y), br = WorldToPart((float)wbr.X, (float)wbr.Y);
      x = tl.X; y = tl.Y; x2 = br.X; yd = br.Y-y+1;
    }

    if(stars==null)
    { stars = new Point3[4000];

      Random r = new Random();
      for(int i=0; i<stars.Length; i++) stars[i] = new Point3(r.Next(-800, 800)/10.0, r.Next(-800, 800)/10.0, r.Next(2500)/100.0);
    }
    GL.glDisable(GL.GL_LIGHTING);
    GL.glDisable(GL.GL_DEPTH_TEST);
    GL.glPointSize(2);
    GL.glBegin(GL.GL_POINTS);
    for(int i=0; i<stars.Length; i++)
    { Point3 pt = stars[i];
      GL.glColor3d(0, 0.15+pt.Z*(0.3/25), 0.31+pt.Z*(0.60/25));
      GL.glVertex3d(pt);
    }
    GL.glEnd();
    GL.glEnable(GL.GL_DEPTH_TEST);
    GL.glEnable(GL.GL_LIGHTING);

    /* TODO: these don't render correctly, but maybe i should just do away with them entirely
    *
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
    }*/

    for(; x<=x2; x++) for(int yi=0; yi<yd; yi++) RenderObjects(x, y+yi);
  }

  public void Update()
  { if(parts.Count>partArr.Length)
    { partArr = new KeyValuePair<System.Drawing.Point,List<SpaceObject>>[parts.Count];
      partArrChanged = true;
    }
    if(partArrChanged)
    { ((System.Collections.ICollection)parts).CopyTo(partArr, 0);
      partArrChanged = false;
    }

    // first, give everything a chance to move
    // FIXME: allow objects to be in all partitions within their radius. this allows collision detection to work
    // correctly in the edge cases. add an ObjFlag to allow us to prevent objects from being updated twice
    for(int pi=0,pcount=parts.Count; pi<pcount; pi++)
    { KeyValuePair<SPoint,List<SpaceObject>> de = partArr[pi];
      List<SpaceObject> objs = de.Value;
      if(objs.Count==0) list.Add(de);
      else
        for(int i=objs.Count-1; i>=0; i--)
        { SpaceObject obj = objs[i];

          if(!obj.Is(ObjFlag.Dead)) obj.Update();

          if(obj.Is(ObjFlag.Dead))
          { objs.RemoveAt(i);
            obj.Map = null;
          }
          else
          { SPoint npart = WorldToPart(obj.X, obj.Y);
            if(de.Key!=npart)
            { objs.RemoveAt(i);
              MakeObjects(npart).Add(obj);
            }
          }
        }
    }

    if(list.Count!=0)
    { foreach(KeyValuePair<SPoint,List<SpaceObject>> de in list) if(de.Value.Count==0) parts.Remove(de.Key);
      list.Clear();
      partArrChanged = true;
    }

    foreach(List<SpaceObject> objs in parts.Values) // now check for collisions
    { objs.Sort(CollisionSort.Instance);
      int count = 0;
      for(; count<objs.Count; count++) if((objs[count].Flags&ObjFlag.HitMask) != ObjFlag.NoHit) break;
      count = objs.Count - count;
      if(objArr.Length<count) objArr = new SpaceObject[count];
      objs.CopyTo(objs.Count-count, objArr, 0, count); // TODO: eliminate this separate array now that we have generics

      int missile=-1, ship=-1, planet=-1;

      for(int i=count-1; i>=0; i--) // first discover the locations of the various types
        switch(objArr[i].Flags&ObjFlag.HitMask)
        { case ObjFlag.Bullet:
            if(missile==-1) missile = i+1;
            if(ship==-1) ship = i+1;
            if(planet==-1) planet = count;
            goto done;
          case ObjFlag.Missile:
            missile = i;
            if(ship==-1) ship = i+1;
            if(planet==-1) planet = count;
            break;
          case ObjFlag.Ship:
            ship = i;
            if(planet==-1) planet = count;
            break;
          case ObjFlag.Planet: planet = i; break;
        }
      if(missile==-1) missile = count;
      if(ship==-1) ship = count;
      done:

      CheckCollisions(0, missile, count);    // check bullet types
      CheckCollisions(missile, ship, count); // and missile types
      CheckCollisions(ship, planet, count);  // and ship types
    }
  }

  public Point WorldToCoord(Point pt)
  { return new Point(Math.Round(pt.X/Factor, 2), Math.Round(pt.Y/Factor, 2)); // note that this assumes Factor is a power of 10
  }

  public SPoint WorldToPart(float x, float y)
  { return new SPoint((int)Math.Floor(x/Factor), (int)Math.Floor(y/Factor));
  }

  sealed class CollisionSort : IComparer<SpaceObject>
  { CollisionSort() { }

    public int Compare(SpaceObject a, SpaceObject b)
    { ObjFlag fa=a.Flags&ObjFlag.HitMask, fb=b.Flags&ObjFlag.HitMask;
      return (int)fa-(int)fb;
    }
    
    public static readonly CollisionSort Instance = new CollisionSort();
  }

  List<SpaceObject> MakeObjects(SPoint pt)
  { List<SpaceObject> list;
    if(!parts.TryGetValue(pt, out list))
    if(list==null)
    { parts[pt] = list = new List<SpaceObject>();
      partArrChanged = true;
    }
    return list;
  }

  void RenderObjects(int x, int y)
  { List<SpaceObject> objs = GetObjects(new SPoint(x, y));
    if(objs==null) return;
    foreach(SpaceObject obj in objs) obj.Render();
  }

  Dictionary<SPoint,List<SpaceObject>> parts = new Dictionary<System.Drawing.Point,List<SpaceObject>>();
  
  static void CheckCollisions(int start, int end, int count)
  { if(start==end || end==count) return;

    for(; start<end; start++)
    { SpaceObject a = objArr[start];
      if(a.Is(ObjFlag.Dead)) continue;
      for(int j=end; j<count; j++)
      { SpaceObject b = objArr[j];
        if(b.Is(ObjFlag.Dead)) continue;
        if(SpaceObject.Collided(a, b)) a.Hit(b);
      }
    }
  }
  
  static List<KeyValuePair<SPoint,List<SpaceObject>>> list = new List<KeyValuePair<System.Drawing.Point,List<SpaceObject>>>();
  static SpaceObject[] objArr = new SpaceObject[0];
  static KeyValuePair<SPoint,List<SpaceObject>>[] partArr = new KeyValuePair<System.Drawing.Point,List<SpaceObject>>[0];
  static bool partArrChanged;
}

} // namespace SpaceWinds