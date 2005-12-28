using System;
using GameLib;
using GameLib.Interop.OpenGL;
using GameLib.Events;
using GameLib.Input;
using GameLib.Video;
using GameLib.Mathematics;
using Point2=GameLib.Mathematics.TwoD.Point;
using Point3=GameLib.Mathematics.ThreeD.Point;
using Vector3=GameLib.Mathematics.ThreeD.Vector;
using SPoint=System.Drawing.Point;
using Quat=GameLib.Mathematics.ThreeD.Quaternion;

namespace SpaceWinds
{

#region App
public sealed class App
{ App() { }

  public const double NearZ=10, FarZ=100, XYSize=5;
  public readonly static string DataPath = "../../data/";

  public enum UIMode { Map, MFD, UI };

  public static UIMode Mode
  { get { return uiMode; }
    set
    { if(value!=uiMode)
      { if(uiMode==UIMode.UI || value==UIMode.UI)
        { ProjectionMatrix = value==UIMode.UI ? uiProjMatrix : mapProjMatrix;
          GL.glMatrixMode(GL.GL_PROJECTION);
          GL.glLoadMatrixd(ProjectionMatrix);
          GL.glMatrixMode(GL.GL_MODELVIEW);
        }

        switch(value)
        { case UIMode.Map: Viewport = mapViewport; break;
          case UIMode.MFD: Viewport = new int[4] { 485, 335, 150, 150 }; break;
          case UIMode.UI: Viewport = uiViewport; break;
        }
        GL.glViewport(Viewport[0], Viewport[1], Viewport[2], Viewport[3]);

        uiMode = value;
      }
    }
  }

  public static double[] ProjectionMatrix;
  public static int[] Viewport;

  public static Point3 Camera;
  public static double Now, TimeDelta;
  public static Player Player;

  public static void Main()
  { Video.Initialize();
    SetMode(640, 480);

    Map map = new Map();

    Player = new Player();
    Player.SetClass("ship2");
    for(int i=0; i<Player.Mounts.Length; i++) Player.Mounts[i].Mounted = new Weapon(new SimpleGun());
    map.Add(Player);
    
    AIShip ai = new AIShip();
    ai.SetClass("ship");
    ai.Pos = new Point2(50, -10);
    for(int i=0; i<ai.Mounts.Length; i++) ai.Mounts[i].Mounted = new Weapon(new SimpleGun());
    map.Add(ai);
    
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
        GL.glLightPosition(GL.GL_LIGHT0, 50, 0, 25, 0);
        map.Render();

        Mode = UIMode.UI;
        GL.glDisable(GL.GL_DEPTH_TEST);
        GL.glDisable(GL.GL_LIGHTING);
        GL.glLoadIdentity();
        GL.glBegin(GL.GL_QUADS);
          GL.glColor3d(32/255.0, 80/255.0, 128/255.0);
          GL.glVertex2i(0, 0);
          GL.glVertex2i(Viewport[2], 0);
          GL.glVertex2i(Viewport[2], Viewport[3]);
          GL.glVertex2i(0, Viewport[3]);
          
          GL.glColor3d(0, 0, 0);
          GL.glVertex2i(5, 5);
          GL.glVertex2i(155, 5);
          GL.glVertex2i(155, 155);
          GL.glVertex2i(5, 155);
        GL.glEnd();

        Mode = UIMode.MFD;
        GL.glEnable(GL.GL_DEPTH_TEST);
        GL.glEnable(GL.GL_LIGHTING);
        GL.glLoadIdentity();
        GL.glLightPosition(GL.GL_LIGHT0, 0, -15, 40, 0);
        GL.glTranslated(0, 0, -14);
        GL.glRotated(60, 1, 0, 0);
        GL.glRotated(ai.Angle * -MathConst.RadiansToDegrees, 0, 0, -1);
        ai.RenderModel();

        Video.Flip();
        Mode = UIMode.Map;
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
    GL.glHint(GL.GL_POINT_SMOOTH_HINT, GL.GL_NICEST);

    GL.glClearColor(0, 0, 0, 1); // misc stuff
    GL.glDisable(GL.GL_DITHER);

    GL.glMatrixMode(GL.GL_PROJECTION); // matrices
    GL.glLoadIdentity();
    GLU.gluOrtho2D(0, Video.Width-Video.Height, Video.Height, 0);
    GL.glGetDoublev(GL.GL_PROJECTION_MATRIX, uiProjMatrix);
    GL.glLoadIdentity();
    GL.glFrustum(-XYSize, XYSize, XYSize, -XYSize, NearZ, FarZ);
    GL.glGetDoublev(GL.GL_PROJECTION_MATRIX, mapProjMatrix);

    GL.glViewport(height, 0, width-height, height);
    GL.glGetIntegerv(GL.GL_VIEWPORT, uiViewport);
    GL.glViewport(0, 0, height, height);
    GL.glGetIntegerv(GL.GL_VIEWPORT, mapViewport);

    GL.glMatrixMode(GL.GL_MODELVIEW);
    GL.glLoadIdentity();

    GL.glShadeModel(GL.GL_SMOOTH); // lighting
    GL.glEnable(GL.GL_LIGHTING);
    GL.glEnable(GL.GL_LIGHT0);
    GL.glLightModeli(GL.GL_LIGHT_MODEL_TWO_SIDE, GL.GL_FALSE);
    
    WM.WindowTitle = "Space Winds";

    ProjectionMatrix = mapProjMatrix;
    Viewport = mapViewport;
    uiMode = UIMode.Map;
  }

  static double[] mapProjMatrix=new double[16], uiProjMatrix=new double[16];
  static int[] mapViewport=new int[4], uiViewport=new int[4];
  static UIMode uiMode;
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

  public static System.IO.Stream LoadData(string path) { return LoadData(path, true); }
  public static System.IO.Stream LoadData(string path, bool throwIfMissing)
  { path = App.DataPath + path;
    if(!throwIfMissing && !System.IO.File.Exists(path)) return null;
    return System.IO.File.Open(path, System.IO.FileMode.Open, System.IO.FileAccess.Read);
  }

  public static System.Xml.XmlDocument LoadXml(string path) { return LoadXml(path, true); }
  public static System.Xml.XmlDocument LoadXml(string path, bool throwIfMissing)
  { System.IO.Stream stream = LoadData(path, throwIfMissing);
    if(stream==null) return null;
    System.Xml.XmlDocument doc = new System.Xml.XmlDocument();
    doc.Load(stream);
    stream.Close();
    return doc;
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