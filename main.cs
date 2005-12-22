using System;
using GameLib;
using GameLib.Mathematics;
using GameLib.Mathematics.TwoD;
using GameLib.Interop.OpenGL;
using GameLib.Events;
using GameLib.Input;
using GameLib.Video;
using Color=System.Drawing.Color;

namespace SpaceWinds
{

public interface IModel
{ void Render();
}

public class TriangleModel : IModel
{ public void Render()
  { GL.glBegin(GL.GL_TRIANGLES);
      GL.glColor(Color.Green);
      GL.glVertex2i(10, 0);
      GL.glVertex2i(-10, 5);
      GL.glVertex2i(-10, -5);
    GL.glEnd();
  }
}

public class Entity
{ public IModel Model;

  public Point Pos;
  public double Speed, Angle;
}

public class Ship : Entity
{ public double MaxSpeed, MaxAccel, TurnSpeed, Throttle;
}

public class Player
{ public Ship Ship;
}

public sealed class App
{ App() { }

  public static void Main()
  { Video.Initialize();
    SetMode(640, 480);
    
    Player p = new Player();
    p.Ship = new Ship();
    p.Ship.MaxAccel = 1;
    p.Ship.MaxSpeed = 250;
    p.Ship.TurnSpeed = Math.PI*2;
    p.Ship.Model = new TriangleModel();
    p.Ship.Pos = new Point(320, 240);

    Events.Initialize();
    Input.Initialize();

    double lastTime = Timing.Seconds;
    while(true)
    { Event e = Events.NextEvent(0);
      if(e!=null)
      { Input.ProcessEvent(e);
        if(Keyboard.Pressed(Key.Escape) || e.Type==EventType.Quit) break;
        if(e.Type==EventType.Exception) throw ((ExceptionEvent)e).Exception;
      }

      double now = Timing.Seconds, time = now-lastTime;

      { double turn=GLMath.AngleBetween(p.Ship.Pos, new Point(Mouse.Point))-p.Ship.Angle, max=p.Ship.TurnSpeed*time;
        if(turn>Math.PI) turn -= Math.PI*2;
        else if(turn<-Math.PI) turn += Math.PI*2;
        if(Math.Abs(turn)>max) turn = max*Math.Sign(turn);

        p.Ship.Angle += turn;
        if(p.Ship.Angle<0) p.Ship.Angle += Math.PI*2;
      }

      if(Keyboard.Pressed(Key.Tab))
      { double accel = p.Ship.MaxAccel*p.Ship.MaxSpeed*2*time;
        p.Ship.Speed = Math.Min(p.Ship.Speed+accel, 500);
      }
      else
      { if(Keyboard.Pressed(Key.Q) || Keyboard.Pressed(Key.A))
        { if(Keyboard.Pressed(Key.Q)) p.Ship.Throttle = Math.Min(p.Ship.Throttle+time, 1);
          else p.Ship.Throttle = Math.Max(p.Ship.Throttle-time, 0);
        }
        if(Keyboard.Pressed(Key.Backquote)) p.Ship.Throttle = 0;

        double accel=p.Ship.Throttle*p.Ship.MaxSpeed-p.Ship.Speed, max=p.Ship.MaxAccel*p.Ship.MaxSpeed*time;
        if(Math.Abs(accel)>max) accel = max*Math.Sign(accel);
        p.Ship.Speed += accel;
      }

      p.Ship.Pos += new Vector(p.Ship.Speed, 0).Rotated(p.Ship.Angle)*time;

      if(p.Ship.Pos.X<0) p.Ship.Pos.X = 0;
      else if(p.Ship.Pos.X>639) p.Ship.Pos.X = 639;

      if(p.Ship.Pos.Y<0) p.Ship.Pos.Y = 0;
      else if(p.Ship.Pos.Y>479) p.Ship.Pos.Y = 479;

      GL.glClear(GL.GL_COLOR_BUFFER_BIT);
      GL.glLoadIdentity();
      GL.glTranslated(p.Ship.Pos.X, p.Ship.Pos.Y, 0);
      GL.glRotated(MathConst.RadiansToDegrees * -p.Ship.Angle, 0, 0, -1); // negate the angle because we're rotating the /camera/
      p.Ship.Model.Render();
      Video.Flip();

      lastTime = now;
    }
  }

  static void SetMode(int width, int height)
  { Video.SetGLMode(width, height, 32, SurfaceFlag.DoubleBuffer);

    GL.glDisable(GL.GL_DITHER);
    GL.glEnable(GL.GL_BLEND);
    GL.glEnable(GL.GL_TEXTURE_2D);
    GL.glBlendFunc(GL.GL_SRC_ALPHA, GL.GL_ONE_MINUS_SRC_ALPHA);
    GL.glHint(GL.GL_PERSPECTIVE_CORRECTION_HINT, GL.GL_FASTEST);
    GL.glShadeModel(GL.GL_SMOOTH);
    GL.glClearColor(0, 0, 0, 0);

    GL.glMatrixMode(GL.GL_PROJECTION);
    GLU.gluOrtho2D(0, width, height, 0);
    GL.glMatrixMode(GL.GL_VIEWPORT);
    GL.glViewport(0, 0, width, height);
    GL.glMatrixMode(GL.GL_MODELVIEW);

    WM.WindowTitle = "Space Winds";
  }
}

} // namespace SpaceWinds