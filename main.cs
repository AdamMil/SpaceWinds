using System;
using System.Runtime.InteropServices;
using GameLib;
using GameLib.Interop.OpenGL;
using GameLib.Events;
using GameLib.Input;
using GameLib.Video;
using GameLib.Mathematics;
using GameLib.Mathematics.TwoD;
using Point2=GameLib.Mathematics.TwoD.Point;
using Point3=GameLib.Mathematics.ThreeD.Point;
using Vector3=GameLib.Mathematics.ThreeD.Vector;
using SPoint=System.Drawing.Point;

namespace SpaceWinds
{

#region Code to get the refresh rate
[StructLayout(LayoutKind.Sequential, Pack=4)]
public struct DeviceMode
{ [MarshalAs(UnmanagedType.ByValTStr, SizeConst=32)] public string DeviceName;
  public ushort SpecVersion, DriverVersion, Size, DriverExtra;
  public uint Fields;
  public short Orientation, PaperSize, PaperLength, PaperWidth, Scale, Copies, DefaultSource, PrintQuality, Color, Duplex,
               YResolution, TTOption, Collate;
  [MarshalAs(UnmanagedType.ByValTStr, SizeConst=32)] public string FormName;
  public ushort UnusedPadding, BitsPerPel;
  public uint PelsWidth, PelsHeight, DisplayFlags, DisplayFrequency;
}

public sealed class SysInterface
{ public static int GetRefreshRate()
  { DeviceMode dm;
    EnumDisplaySettings(null, unchecked((uint)-1), out dm);
    return (int)dm.DisplayFrequency;
  }
  
  [DllImport("user32.dll")]
  static extern int EnumDisplaySettings(string deviceName, uint mode, out DeviceMode dm);
}
#endregion

#region App
public sealed class App
{ App() { }

  public const float NearZ=10, FarZ=200, SizeAtNear=2, MinDistFromPlanes=10;
  public readonly static string DataPath = "../../data/";

  public static double[] ProjectionMatrix;
  public static int[] Viewport;

  public static Point3 Camera;
  public static float Now, TimeDelta;
  public static Player Player;

  #region Main
  public static void Main()
  { Video.Initialize();
    SetMode(800, 600, fullScreen=false);
    ObjMaterial.LoadLibrary("global.mtl");

    try
    { Map map = new Map();
      Player = new Player();
      Player.SetClass("ship2");
      for(int i=0; i<Player.Mounts.Length; i++) Player.Mounts[i].Mounted = new Weapon(new SimpleGun());
      map.Add(Player);
      
      AIShip ai = new AIShip();
      ai.SetClass("ship");
      ai.Pos = new Point2(150, -40);
      for(int i=0; i<ai.Mounts.Length; i++) ai.Mounts[i].Mounted = new Weapon(new SimpleGun());
      map.Add(ai);

      Planet earth = new Planet();
      earth.Model = Model.Load("planet_earth");
      earth.Pos = new Point2(10, 10);
      earth.SetAxis(new GameLib.Mathematics.ThreeD.Quaternion(new Vector3(1, 0, 0), 15*MathConst.DegreesToRadians) *
                    new GameLib.Mathematics.ThreeD.Quaternion(new Vector3(0, 1, 0), 15*MathConst.DegreesToRadians));
      earth.RotateSpeed = (float)(Math.PI/64);
      map.Add(earth);

      Events.Initialize();
      Input.Initialize();

      float lastTime = (float)Timing.Seconds, zoom = NearZ+(FarZ-NearZ)/2;

      while(true)
      { Event e = Events.NextEvent(0);
        if(e!=null)
        { if(Input.ProcessEvent(e))
          { if(e.Type==EventType.MouseClick)
            { MouseClickEvent mc = (MouseClickEvent)e;
              if(mc.Button==MouseButton.WheelDown) zoom = Math.Min(FarZ-MinDistFromPlanes, zoom+5);
              else if(mc.Button==MouseButton.WheelUp) zoom = Math.Max(NearZ+MinDistFromPlanes, zoom-5);
            }
            else if(Keyboard.Pressed(Key.Escape)) break;
          }
          else if(e.Type==EventType.Resize)
          { ResizeEvent re = (ResizeEvent)e;
            SetMode(re.Width, re.Height, fullScreen);
          }
          else if(e.Type==EventType.Quit) break;
          else if(e.Type==EventType.Exception) throw ((ExceptionEvent)e).Exception;
        }

        Now       = (float)Timing.Seconds;
        TimeDelta = Now-lastTime;
        lastTime  = Now;

        GL.glLoadIdentity();
        GL.glTranslated(-Player.X, -Player.Y, -zoom);
        map.Update();

        if(WM.Active)
        { Camera = new Point3(Player.X, Player.Y, zoom);
          GL.glLoadIdentity();
          GL.glTranslated(-Camera.X, -Camera.Y, -Camera.Z);
          GL.glClear(GL.GL_COLOR_BUFFER_BIT | GL.GL_DEPTH_BUFFER_BIT);
          GL.glLightPosition(GL.GL_LIGHT0, 50, 0, 25, 0);
          map.Render();

          Mode = UIMode.UI;
          Material.Current = null;
          GL.glDisable(GL.GL_DEPTH_TEST);
          GL.glDisable(GL.GL_LIGHTING);
          GL.glDisable(GL.GL_CULL_FACE);
          GL.glLoadIdentity();
          GL.glBegin(GL.GL_QUADS);
          { GL.glColor3d(32/255.0, 80/255.0, 128/255.0);
            GL.glVertex2i(0, 0);
            GL.glVertex2i(Viewport[2], 0);
            GL.glVertex2i(Viewport[2], Viewport[3]);
            GL.glVertex2i(0, Viewport[3]);
            
            int x=mfdViewport[0]-Viewport[0], y=Viewport[3]-mfdViewport[1]-mfdViewport[3];
            GL.glColor3d(0, 0, 0);
            GL.glVertex2i(x, y);
            GL.glVertex2i(x+mfdViewport[2], y);
            GL.glVertex2i(x+mfdViewport[2], y+mfdViewport[3]);
            GL.glVertex2i(x, y+mfdViewport[3]);
          }
          GL.glEnd();

          Mode = UIMode.MFD;
          GL.glEnable(GL.GL_DEPTH_TEST);
          GL.glEnable(GL.GL_LIGHTING);
          GL.glEnable(GL.GL_CULL_FACE);
          GL.glLoadIdentity();
          GL.glLightPosition(GL.GL_LIGHT0, 0, -15, 40, 0);
          GL.glTranslated(0, 0, -NearZ-SizeAtNear);
          GL.glRotated(60, 1, 0, 0);
          GL.glRotated(ai.Angle * MathConst.RadiansToDegrees, 0, 0, 1);
          ai.RenderModel();

          UpdateDivisor((float)Timing.Seconds-lastTime);
          while((float)Timing.Seconds-lastTime<frameTime) { }

          Video.Flip();
          Mode = UIMode.Map;
        }
      }
    }
    finally { Texture.FreeAll(); }
  }
  #endregion

  #region UIMode
  enum UIMode { Map, MFD, UI };

  static UIMode Mode
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
          case UIMode.MFD: Viewport = mfdViewport; break;
          case UIMode.UI: Viewport = uiViewport; break;
        }
        GL.glViewport(Viewport[0], Viewport[1], Viewport[2], Viewport[3]);

        uiMode = value;
      }
    }
  }
  #endregion

  #region NextDivisor and PreviousDivisor
  static int NextDivisor()
  { if(divisor==frameRate) return divisor;
    for(int i=divisor+1; i<=frameRate; i++) if(frameRate/i*i==frameRate) return i;
    return divisor;
  }

  static int PreviousDivisor()
  { if(divisor==1) return 1;
    for(int i=divisor-1; i>=1; i--) if(frameRate/i*i==frameRate) return i;
    return divisor;
  }
  #endregion

  #region SetDivisor
  static void SetDivisor(int div)
  { if(div==divisor) return;
    if(frameTimings==null || frameTimings.Length<frameRate/div) frameTimings = new float[frameRate/div];
    frameCount  = unreliability = ftIndex = 0;
    divisor     = div;
    frameTime   = (float)((double)div/frameRate*0.93);
    frameFast   = div==1 ? 0 : (float)((double)PreviousDivisor()/frameRate*0.93);
    ftThreshold = frameRate/15;
Console.WriteLine("Divisor set to "+div.ToString());
Console.WriteLine("frameTime: "+frameTime.ToString());
  }
  #endregion

  #region SetMode
  static void SetMode(int width, int height, bool fullScreen)
  { Video.SetGLMode(width, height, 32, SurfaceFlag.DoubleBuffer | (fullScreen ? SurfaceFlag.Fullscreen : 0));
    frameRate = SysInterface.GetRefreshRate();
    SetDivisor(1);

    GL.glEnable(GL.GL_DEPTH_TEST); // z-buffering
    GL.glDepthFunc(GL.GL_LEQUAL);

    GL.glBlendFunc(GL.GL_SRC_ALPHA, GL.GL_ONE_MINUS_SRC_ALPHA); // alpha blending

    GL.glDisable(GL.GL_TEXTURE_2D); // texture mapping
    GL.glHint(GL.GL_PERSPECTIVE_CORRECTION_HINT, GL.GL_NICEST);

    GL.glDisable(GL.GL_POINT_SMOOTH);
    GL.glEnable(GL.GL_POLYGON_SMOOTH);
    GL.glHint(GL.GL_POLYGON_SMOOTH_HINT, GL.GL_NICEST);
    GL.glHint(GL.GL_POINT_SMOOTH_HINT, GL.GL_NICEST);

    GL.glClearColor(0, 0, 0, 1); // misc stuff
    GL.glDisable(GL.GL_DITHER);
    GL.glEnable(GL.GL_CULL_FACE);

    GL.glMatrixMode(GL.GL_PROJECTION); // matrices
    GL.glLoadIdentity();
    GLU.gluOrtho2D(0, Video.Width-Video.Height, Video.Height, 0);
    GL.glGetDoublev(GL.GL_PROJECTION_MATRIX, uiProjMatrix);
    GL.glLoadIdentity();
    GL.glFrustum(-SizeAtNear, SizeAtNear, SizeAtNear, -SizeAtNear, NearZ, FarZ);
    GL.glGetDoublev(GL.GL_PROJECTION_MATRIX, mapProjMatrix);

    GL.glViewport(height, 0, width-height, height);
    GL.glGetIntegerv(GL.GL_VIEWPORT, uiViewport);
    GL.glViewport(0, 0, height, height);
    GL.glGetIntegerv(GL.GL_VIEWPORT, mapViewport);

    mfdViewport[2] = mfdViewport[3] = uiViewport[2]-10;
    mfdViewport[0] = uiViewport[0]+5;
    mfdViewport[1] = uiViewport[3]-mfdViewport[3]-5;

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
  #endregion

  #region UpdateDivisor
  static void UpdateDivisor(float time)
  { if(frameCount++<0) frameCount = frameTimings.Length; // handle overflow

    if(frameCount<frameTimings.Length)
    { frameTimings[ftIndex++] = time;
      if(time>frameTime) { unreliability++; Console.WriteLine("+ "+time.ToString()); }
      else if(time<frameFast) { unreliability--; Console.WriteLine("- "+time.ToString()); }
    }
    else
    { if(ftIndex==frameTimings.Length) ftIndex = 0;

      if(time>frameTime) { unreliability++; Console.WriteLine("+ "+time.ToString()); }
      else if(time<frameFast) { unreliability--; Console.WriteLine("- "+time.ToString()); }

      float oldTime = frameTimings[ftIndex];
      if(oldTime>frameTime) { unreliability--; Console.WriteLine("/ "+time.ToString()); }
      else if(oldTime<frameFast) { unreliability++; Console.WriteLine("* "+time.ToString()); }

      frameTimings[ftIndex++] = time;

      if(Math.Abs(unreliability)>ftThreshold && (unreliability<0 || divisor!=frameRate))
        SetDivisor(unreliability<0 ? PreviousDivisor() : NextDivisor());

      if(frameCount%(frameRate*2)==0)
      { float avg = 0;
        for(int i=0; i<frameTimings.Length; i++) avg += frameTimings[i];
        Console.WriteLine("Average time: "+(avg/frameTimings.Length).ToString());
        Console.WriteLine("Unreliability: "+unreliability.ToString());
      }
    }
  }
  #endregion

  static double[] mapProjMatrix=new double[16], uiProjMatrix=new double[16];
  static int[] mapViewport=new int[4], uiViewport=new int[4], mfdViewport=new int[4];
  static float[] frameTimings;
  static int frameRate, frameCount, divisor, unreliability, ftIndex, ftThreshold;
  static float frameTime, frameFast;
  static UIMode uiMode;
  static bool fullScreen;
}
#endregion

#region Misc
public sealed class Misc
{ Misc() { }

  public static float AngleBetween(Point2 a, Point2 b) { return (float)Math2D.AngleBetween(a, b); }
  public static float AngleBetween(Point2 a, Point3 b) { return (float)Math2D.AngleBetween(a, new Point2(b.X, b.Y)); }
  public static float AngleBetween(Point3 a, Point2 b) { return (float)Math2D.AngleBetween(new Point2(a.X, a.Y), b); }
  public static float AngleBetween(Point3 a, Point3 b)
  { return (float)Math2D.AngleBetween(new Point2(a.X, b.X), new Point2(b.X, b.Y));
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

  public static float NormalizeAngle(float angle)
  { return angle<0 ? angle+(float)(Math.PI*2) : angle>=(float)Math.PI*2 ? angle-(float)(Math.PI*2) : angle;
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