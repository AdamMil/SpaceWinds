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

public sealed class Global
{ Global() { }

  public static double NormalizeAngle(double angle)
  { return angle<0 ? angle+Math.PI*2 : angle>=Math.PI*2 ? angle-Math.PI*2 : angle;
  }
}

public enum MountType : byte { Weapon }

public abstract class Mountable
{ public abstract void Fire(Ship owner, ref Mount mount);
  public abstract void Render();
}

public class Bullet : SpaceObject
{ public Bullet(Point pos, Vector vel) { Pos=pos; Velocity=vel; Born=App.Now; }

  public override void Render()
  { GL.glLoadIdentity();
    GL.glBegin(GL.GL_POINTS);
      GL.glColor(Color.White);
      GL.glVertex2d(Pos.X, Pos.Y);
    GL.glEnd();
  }

  public override void Update(double time)
  { if(App.Now-Born>1.5) Dead = true;
    else Pos += Velocity*time;
  }
  
  public Vector Velocity;
  public double Born;
}

public abstract class Weapon : Mountable
{ protected bool TryFire()
  { if(App.Now-LastFire>=ReloadTime)
    { if(Ammo==-1) return true;
      if(Ammo!=0) { Ammo--; return true; }
    }
    return false;
  }

  public double ReloadTime, LastFire;
  public int Ammo;
}

public class SimpleGun : Weapon
{ public SimpleGun() { Ammo=-1; ReloadTime=0.01; }

  public override void Fire(Ship owner, ref Mount mount)
  { if(TryFire())
    { double gunAngle = owner.Angle+mount.CenterAngle+mount.Offset;
      App.objects.Add(new Bullet(owner.Pos+new Vector(mount.X, mount.Y).Rotated(owner.Angle)+new Vector(3, 0).Rotated(gunAngle),
                                 new Vector(owner.Speed, 0).Rotated(owner.Angle)+new Vector(500, 0).Rotated(gunAngle)));
      LastFire = App.Now;
    }
  }

  public override void Render()
  { GL.glBegin(GL.GL_LINES);
      GL.glColor(Color.White);
      GL.glVertex2i(0, 0);
      GL.glVertex2i(5, 0);
    GL.glEnd();
  }
}

public struct Mount
{ public void Fire(Ship owner) { if(Mounted!=null) Mounted.Fire(owner, ref this); }

  public void Render()
  { if(Mounted==null) return;
    GL.glPushMatrix();
    GL.glTranslated(X, Y, 0);
    GL.glRotated(-MathConst.RadiansToDegrees * (CenterAngle+Offset), 0, 0, -1);
    Mounted.Render();
    GL.glPopMatrix();
  }

  public void TurnTowards(double desiredAngle, double time)
  { if(MaxTurn==0) { Offset=0; return; }

    double turn, max;

    if(MaxTurn==Math.PI*2)
    { turn = desiredAngle-(CenterAngle+Offset);
      if(turn>Math.PI) turn -= Math.PI*2;
      else if(turn<-Math.PI) turn += Math.PI*2;
    }
    else
    { double min = Global.NormalizeAngle(CenterAngle-MaxTurn);
      max = Global.NormalizeAngle(CenterAngle+MaxTurn);
      if(min<max)
      { if(desiredAngle<min) desiredAngle = min;
        else if(desiredAngle>max) desiredAngle = max;
      }
      else if(desiredAngle<min && desiredAngle>max)
        desiredAngle = Math.Abs(desiredAngle-min)<Math.Abs(desiredAngle-max) ? min : max;

      min = CenterAngle + Math.PI; // hijack 'min' to be the opposite of the center angle
      if(min>=Math.PI*2) min -= Math.PI*2;

      double cur = CenterAngle+Offset;
      turn = desiredAngle - cur;

      cur = Global.NormalizeAngle(cur-min);
      desiredAngle = Global.NormalizeAngle(desiredAngle-min);

      if(cur<desiredAngle)
      { if(turn<0) turn += Math.PI*2;
      }
      else if(turn>0) turn -= Math.PI*2;
    }

    max = TurnSpeed*time;
    if(Math.Abs(turn)>max) turn = max*Math.Sign(turn);

    Offset += turn;
    if(Offset<-Math.PI*2) Offset += Math.PI*2;
    else if(Offset>Math.PI*2) Offset -= Math.PI*2;
  }

  public Mountable Mounted;
  public double X, Y, CenterAngle, Offset, MaxTurn, TurnSpeed;
  public MountType Type;
  public byte MaxLevel;
}

public abstract class Model
{ public abstract void Render();
}

public class TriangleModel : Model
{ public override void Render()
  { GL.glBegin(GL.GL_TRIANGLES);
      GL.glColor(Color.Green);
      GL.glVertex2i(10, 0);
      GL.glVertex2i(-10, 5);
      GL.glVertex2i(-10, -5);
    GL.glEnd();
  }
}

public abstract class SpaceObject
{ public abstract void Render();
  public abstract void Update(double time);

  public Point Pos;
  public bool Dead;
}

public abstract class Ship : SpaceObject
{ public override void Render()
  { GL.glLoadIdentity();
    GL.glTranslated(Pos.X, Pos.Y, 0);
    GL.glRotated(-MathConst.RadiansToDegrees * Angle, 0, 0, -1); // negate the angle because we're rotating the /camera/

    Model.Render();
    if(Mounts!=null) for(int i=0; i<Mounts.Length; i++) Mounts[i].Render();
  }

  public void AimAt(Point pt, double time) { AimAt(GLMath.AngleBetween(Pos, pt), time); }
  public void AimAt(double desiredAngle, double time)
  { if(Mounts!=null)
    { desiredAngle = Global.NormalizeAngle(desiredAngle-Angle);
      for(int i=0; i<Mounts.Length; i++) Mounts[i].TurnTowards(desiredAngle, time);
    }
  }

  public void TurnTowards(Point pt, double time) { TurnTowards(GLMath.AngleBetween(Pos, pt), time); }
  public void TurnTowards(double desiredAngle, double time)
  { double turn=desiredAngle-Angle, max=TurnSpeed*time;
    if(turn>Math.PI) turn -= Math.PI*2;
    else if(turn<-Math.PI) turn += Math.PI*2;
    if(Math.Abs(turn)>max) turn = max*Math.Sign(turn);

    Angle = Global.NormalizeAngle(Angle+turn);
  }

  public Mount[] Mounts;
  public Model Model;
  public double Speed, Angle, MaxSpeed, MaxAccel, TurnSpeed, Throttle;
}

public class Player : Ship
{ // TODO: process events rather than poll the devices, so nothing gets lost between calls to Update()
  public override void Update(double time)
  { if(Mouse.PressedRel(MouseButton.Right)) turnTowardsCursor = !turnTowardsCursor;

    { double angle = GLMath.AngleBetween(Pos, Mouse.Point);
      if(turnTowardsCursor) TurnTowards(angle, time);
      AimAt(angle, time);
    }

    if(Keyboard.Pressed(Key.Tab))
    { double accel = MaxAccel*MaxSpeed*2*time;
      Speed = Math.Min(Speed+accel, 500);
    }
    else
    { if(Keyboard.Pressed(Key.Q) || Keyboard.Pressed(Key.A))
      { if(Keyboard.Pressed(Key.Q)) Throttle = Math.Min(Throttle+time, 1);
        else Throttle = Math.Max(Throttle-time, 0);
      }
      if(Keyboard.PressedRel(Key.Backquote)) Throttle = 0;

      double accel=Throttle*MaxSpeed-Speed, max=MaxAccel*MaxSpeed*time;
      if(Math.Abs(accel)>max) accel = max*Math.Sign(accel);
      Speed += accel;
    }

    Pos += new Vector(Speed, 0).Rotated(Angle)*time;

    if(Mounts!=null && Mouse.Pressed(MouseButton.Left)) for(int i=0; i<Mounts.Length; i++) Mounts[i].Fire(this);

    if(Pos.X<0) Pos.X = 0;
    else if(Pos.X>639) Pos.X = 639;

    if(Pos.Y<0) Pos.Y = 0;
    else if(Pos.Y>479) Pos.Y = 479;
  }
  
  bool turnTowardsCursor;
}

public sealed class App
{ App() { }

  public static double Now;
  public static System.Collections.ArrayList objects = new System.Collections.ArrayList();
  public static void Main()
  { Video.Initialize();
    SetMode(640, 480);
    
    Player p = new Player();
    p.MaxAccel = 1;
    p.MaxSpeed = 250;
    p.TurnSpeed = Math.PI;
    p.Model = new TriangleModel();
    p.Pos = new Point(320, 240);
    
    p.Mounts = new Mount[3];
    p.Mounts[0].MaxTurn = Math.PI/2;
    p.Mounts[0].Mounted = new SimpleGun();
    p.Mounts[0].TurnSpeed = Math.PI*6;
    p.Mounts[0].X = 10;

    p.Mounts[1] = p.Mounts[2] = p.Mounts[0];
    p.Mounts[1].CenterAngle = Math.PI*3/2;
    p.Mounts[1].X = -10;
    p.Mounts[1].Y = -5;
    p.Mounts[1].Mounted = new SimpleGun();

    p.Mounts[2].CenterAngle = Math.PI/2;
    p.Mounts[2].X = -10;
    p.Mounts[2].Y = 5;
    p.Mounts[2].Mounted = new SimpleGun();

    objects.Add(p);

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

      Now = Timing.Seconds;
      double diff = Now-lastTime;

      for(int i=objects.Count-1; i>=0; i--)
      { SpaceObject o = (SpaceObject)objects[i];
        if(o.Dead) objects.RemoveAt(i);
        else o.Update(diff);
      }

      lastTime = Now;

      GL.glClear(GL.GL_COLOR_BUFFER_BIT);
      foreach(SpaceObject o in objects) o.Render();
      Video.Flip();
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