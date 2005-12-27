using System;
using GameLib;
using GameLib.Interop.OpenGL;
using GameLib.Events;
using GameLib.Input;
using GameLib.Video;
using GameLib.Mathematics;
using Point2=GameLib.Mathematics.TwoD.Point;
using Point3=GameLib.Mathematics.ThreeD.Point;
using SPoint=System.Drawing.Point;

namespace SpaceWinds
{


#region App
public sealed class App
{ App() { }

  public const double NearZ=10, FarZ=100, XYSize=5;
  public readonly static string DataPath = "../../data/work/";

  // these are for the map view only
  public static double[] ProjectionMatrix=new double[16];
  public static int[] Viewport=new int[4];

  public static Point3 Camera;
  public static double Now, TimeDelta;
  public static Player Player;

  public static void Main()
  { Video.Initialize();
    SetMode(640, 480);

    Player = new Player();
    Player.MaxAccel = 1.25;
    Player.MaxSpeed = 10;
    Player.TurnSpeed = Math.PI;
    ObjModel ship = new ObjModel("ship");
    Player.Model = ship;
    Player.Mounts = ship.Mounts;
    Player.Mounts[0].Mounted = new Weapon(new SimpleGun());
    Player.Mounts[1].Mounted = new Weapon(new SimpleGun());
    Player.Mounts[2].Mounted = new Weapon(new SimpleGun());

    Map map = new Map();
    map.Add(Player);

    Events.Initialize();
    Input.Initialize();

    double lastTime = Timing.Seconds, zoom = (FarZ-NearZ)/2+NearZ;
    while(true)
    { Event e = Events.NextEvent(0);
      if(e!=null)
      { if(Input.ProcessEvent(e))
        { if(e.Type==EventType.MouseClick)
          { MouseClickEvent mc = (MouseClickEvent)e;
            if(mc.Button==MouseButton.WheelDown) zoom = Math.Min(FarZ-XYSize, zoom+XYSize);
            else if(mc.Button==MouseButton.WheelUp) zoom = Math.Max(NearZ+XYSize, zoom-XYSize);
          }
          else if(Keyboard.Pressed(Key.Escape)) break;
        }
        else if(e.Type==EventType.Resize)
        { ResizeEvent re = (ResizeEvent)e;
          SetMode(re.Width, re.Height);
        }
        else if(e.Type==EventType.Quit) break;
        else if(e.Type==EventType.Exception) throw ((ExceptionEvent)e).Exception;
      }

      Now       = Timing.Seconds;
      TimeDelta = Now-lastTime;
      lastTime  = Now;
      
      Camera = new Point3(Player.Pos.X, Player.Pos.Y, zoom);
      GL.glLoadIdentity();
      GL.glTranslated(-Camera.X, -Camera.Y, -Camera.Z);
      map.Update();

      if(WM.Active)
      { GL.glClear(GL.GL_COLOR_BUFFER_BIT | GL.GL_DEPTH_BUFFER_BIT);
        //GL.glMatrixMode(GL.GL_VIEWPORT);
        //GL.glViewport(0, 0, Video.Height, Video.Height);
        //GL.glMatrixMode(GL.GL_MODELVIEW);
        map.Render();
        Video.Flip();
      }
    }
  }

  static void SetMode(int width, int height)
  { Video.SetGLMode(width, height, 32, SurfaceFlag.DoubleBuffer);

    GL.glEnable(GL.GL_DEPTH_TEST); // z-buffering
    GL.glDepthFunc(GL.GL_LEQUAL);

    GL.glBlendFunc(GL.GL_SRC_ALPHA, GL.GL_ONE_MINUS_SRC_ALPHA); // alpha blending

    GL.glEnable(GL.GL_TEXTURE_2D); // texture mapping
    GL.glHint(GL.GL_PERSPECTIVE_CORRECTION_HINT, GL.GL_NICEST);

    GL.glEnable(GL.GL_POLYGON_SMOOTH);
    GL.glHint(GL.GL_POLYGON_SMOOTH_HINT, GL.GL_NICEST);

    GL.glClearColor(0, 0, 0, 1); // misc stuff
    GL.glDisable(GL.GL_DITHER);

    GL.glMatrixMode(GL.GL_PROJECTION); // matrices
    GL.glFrustum(-XYSize, XYSize, XYSize, -XYSize, NearZ, FarZ);
    GL.glGetDoublev(GL.GL_PROJECTION_MATRIX, ProjectionMatrix);

    GL.glMatrixMode(GL.GL_VIEWPORT);
    GL.glViewport(0, 0, width, height);
    GL.glGetIntegerv(GL.GL_VIEWPORT, Viewport);

    GL.glMatrixMode(GL.GL_MODELVIEW);
    GL.glLoadIdentity();

    GL.glShadeModel(GL.GL_SMOOTH); // lighting
    GL.glEnable(GL.GL_LIGHTING);
    GL.glEnable(GL.GL_LIGHT0);
    GL.glLightModeli(GL.GL_LIGHT_MODEL_TWO_SIDE, GL.GL_FALSE);
    GL.glLightPosition(GL.GL_LIGHT0, 50, 0, 25, 0);
    
    WM.WindowTitle = "Space Winds";
  }
}
#endregion

#region Misc
public sealed class Misc
{ Misc() { }

  public static double AngleBetween(Point2 a, Point3 b) { return GLMath.AngleBetween(a, new Point2(b.X, b.Y)); }
  public static double AngleBetween(Point3 a, Point2 b) { return GLMath.AngleBetween(new Point2(a.X, a.Y), b); }
  public static double AngleBetween(Point3 a, Point3 b)
  { return GLMath.AngleBetween(new Point2(a.X, b.X), new Point2(b.X, b.Y));
  }

  public static double NormalizeAngle(double angle)
  { return angle<0 ? angle+Math.PI*2 : angle>=Math.PI*2 ? angle-Math.PI*2 : angle;
  }
  
  public static Point2 Project(Point2 pt) { return Project(new Point3(pt.X, pt.Y, App.NearZ)); }
  public static Point2 Project(Point3 pt)
  { double wx, wy, wz;
    GL.glGetDoublev(GL.GL_MODELVIEW_MATRIX, modelMatrix);
    GLU.gluProject(pt.X, pt.Y, pt.Z, modelMatrix, App.ProjectionMatrix, App.Viewport, out wx, out wy, out wz);
    return new Point2(wx, App.Viewport[3]-wy);
  }

  public static Point3 Unproject(SPoint pt) { return Unproject(pt, 0); }
  public static Point3 Unproject(SPoint pt, double worldZ)
  { double ox, oy, oz, ox2, oy2, oz2, factor;
    GL.glGetDoublev(GL.GL_MODELVIEW_MATRIX, modelMatrix);
    GLU.gluUnProject(pt.X, App.Viewport[3]-pt.Y, 0, modelMatrix, App.ProjectionMatrix, App.Viewport,
                     out ox, out oy, out oz);
    GLU.gluUnProject(pt.X, App.Viewport[3]-pt.Y, 1, modelMatrix, App.ProjectionMatrix, App.Viewport,
                     out ox2, out oy2, out oz2);
    factor = (worldZ-oz)/(oz2-oz);
    return new Point3(ox+(ox2-ox)*factor, oy+(oy2-oy)*factor, worldZ);
  }
  
  public readonly static double[] modelMatrix = new double[16];
}
#endregion


} // namespace SpaceWinds