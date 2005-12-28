using System;
using System.Collections;
using System.Xml;
using GameLib.Events;
using GameLib.Input;
using GameLib.Interop.OpenGL;
using GameLib.Mathematics;
using GameLib.Mathematics.TwoD;
using Point3=GameLib.Mathematics.ThreeD.Point;
using Vector3=GameLib.Mathematics.ThreeD.Vector;

namespace SpaceWinds
{

public enum MountType : byte { Weapon }
[Flags] public enum ObjFlag { Dead=1 }

#region Mount
public struct Mount
{ public void Fire(Ship owner) { if(Mounted!=null) Mounted.Fire(owner, ref this); }

  public void Render()
  { if(Mounted==null || Class.Model==null) return;
    GL.glPushMatrix();
    GL.glTranslated(Class.RenderOffset);
    GL.glRotated(-MathConst.RadiansToDegrees * (Class.CenterAngle+Offset), 0, 0, -1);
    Class.Model.Render();
    GL.glPopMatrix();
  }

  public void TurnTowards(double desiredAngle)
  { if(Mounted==null) return;
    if(Class.MaxTurn==0) { Offset=0; return; }

    double turn, max;
    desiredAngle = Misc.NormalizeAngle(desiredAngle);

    if(Class.MaxTurn==Math.PI)
    { turn = desiredAngle-(Class.CenterAngle+Offset);
      if(turn>Math.PI) turn -= Math.PI*2;
      else if(turn<-Math.PI) turn += Math.PI*2;
    }
    else
    { double min = Misc.NormalizeAngle(Class.CenterAngle-Class.MaxTurn);
      max = Misc.NormalizeAngle(Class.CenterAngle+Class.MaxTurn);
      if(min<max)
      { if(desiredAngle<min) desiredAngle = min;
        else if(desiredAngle>max) desiredAngle = max;
      }
      else if(desiredAngle<min && desiredAngle>max)
        desiredAngle = Math.Abs(desiredAngle-min)<Math.Abs(desiredAngle-max) ? min : max;

      min = Class.CenterAngle + Math.PI; // hijack 'min' to be the opposite of the center angle
      if(min>=Math.PI*2) min -= Math.PI*2;

      double cur = Class.CenterAngle + Offset;
      turn = desiredAngle - cur;
      if(turn<=-Math.PI*2) turn += Math.PI*2;
      else if(turn>=Math.PI*2) turn -= Math.PI*2;
      if(Math.Abs(turn)<0.000001) { return; } // prevent ugly turret jitter (due to FP inaccuracy) and also optimize the turn==0 case
      if(Class.TurnSpeed==0) goto turn;

      cur = Misc.NormalizeAngle(cur-min);
      desiredAngle = Misc.NormalizeAngle(desiredAngle-min);

      if(cur<desiredAngle)
      { if(turn<0) turn += Math.PI*2;
      }
      else if(turn>0) turn -= Math.PI*2;
    }

    max = Class.TurnSpeed*App.TimeDelta;
    if(Math.Abs(turn)>max) turn = max*Math.Sign(turn);

    turn:
    Offset += turn;
    if(Offset<-Math.PI*2) Offset += Math.PI*2;
    else if(Offset>Math.PI*2) Offset -= Math.PI*2;
  }

  public MountClass Class;
  public Mountable Mounted;
  public double Offset;
}
#endregion

#region MountClass
public sealed class MountClass
{ public double CenterAngle, MaxTurn, TurnSpeed;
  public Vector3 RenderOffset;
  public Vector  FireOffset;
  public Model Model;
  public MountType Type;
  public byte MaxLevel;
}
#endregion

#region Mountable
public abstract class Mountable
{ public abstract void Fire(Ship owner, ref Mount mount);

  protected void GetInfo(Ship owner, ref Mount mount, out Point startPoint, out Vector baseVelocity, out double angle)
  { angle = owner.Angle + mount.Class.CenterAngle + mount.Offset;
    startPoint = owner.Pos + new Vector(mount.Class.RenderOffset.X, mount.Class.RenderOffset.Y).Rotated(owner.Angle) +
                 mount.Class.FireOffset.Rotated(angle);
    baseVelocity = new Vector(owner.Speed, 0).Rotated(owner.Angle);
  }
}
#endregion

#region Weapon
public sealed class Weapon : Mountable
{ public Weapon(WeaponClass wclass) { Class=wclass; Ammo=wclass.MaxAmmo; }

  public override void Fire(Ship owner, ref Mount mount)
  { if(TryFire())
    { Point  startPoint;
      Vector baseVelocity;
      double gunAngle;
      GetInfo(owner, ref mount, out startPoint, out baseVelocity, out gunAngle);
      owner.Map.Add(Class.CreateProjectile(startPoint, baseVelocity, gunAngle)); // TODO: support non-projectile (eg laser) weapons
    }
  }

  public double LastFired;
  public WeaponClass Class;
  public int Ammo;

  bool TryFire()
  { if(App.Now-LastFired>=Class.ReloadTime)
    { if(Ammo==-1) { LastFired=App.Now; return true; }
      if(Ammo!=0) { LastFired=App.Now; Ammo--; return true; }
    }
    return false;
  }
}
#endregion

#region WeaponClass
public abstract class WeaponClass
{ public abstract SpaceObject CreateProjectile(Point startPoint, Vector baseVelocity, double gunAngle);
  public double ReloadTime;
  public int MaxAmmo;
}
#endregion

public sealed class SimpleGun : WeaponClass
{ public SimpleGun() { MaxAmmo=-1; ReloadTime=1; }

  public override SpaceObject CreateProjectile(Point startPoint, Vector baseVelocity, double gunAngle)
  { return new Bullet(startPoint, baseVelocity+new Vector(50, 0).Rotated(gunAngle));
  }
}

public sealed class Bullet : SpaceObject
{ public Bullet(Point pos, Vector vel) { Pos=pos; Angle=vel.Angle; Velocity=vel; Born=App.Now; }

  public override void RenderModel()
  { GL.glDisable(GL.GL_LIGHTING);
    GL.glPointSize(3);
    GL.glEnable(GL.GL_POINT_SMOOTH);
    GL.glBegin(GL.GL_POINTS);
      GL.glColor(System.Drawing.Color.White);
      GL.glVertex2d(0, 0);
    GL.glEnd();
    GL.glEnable(GL.GL_LIGHTING);
  }

  public override void Update()
  { if(App.Now-Born>1.5) Set(ObjFlag.Dead, true);
    else Pos += Velocity*App.TimeDelta;
  }
  
  public Vector Velocity;
  public double Born;
}

#region SpaceObject
public abstract class SpaceObject
{ public bool Is(ObjFlag flag) { return (Flags&flag)!=0; }
  public void Set(ObjFlag flag, bool on)
  { if(on) Flags |= flag;
    else Flags &= ~flag;
  }

  public void Render()
  { GL.glPushMatrix();
    GL.glTranslated(Pos.X, Pos.Y, 0);
    GL.glRotated(-MathConst.RadiansToDegrees * Angle, 0, 0, -1); // negate the angle because we're rotating the /camera/
    RenderModel();
    GL.glPopMatrix();
  }

  public virtual void RenderModel() { Model.Render(); }

  public abstract void Update();

  public Map Map;
  public Model Model;
  public Point Pos;
  public double Angle;
  public ObjFlag Flags;
}
#endregion

#region MountsObject
public abstract class MountsObject : SpaceObject
{ public void AimAt(Point pt)
  { if(Mounts!=null)
    { Point3 opt = new Point3(Pos.X, Pos.Y, 0);
      for(int i=0; i<Mounts.Length; i++)
        Mounts[i].TurnTowards(Misc.AngleBetween(opt+Mounts[i].Class.RenderOffset.RotatedZ(Angle), pt) - Angle);
    }
  }

  public override void RenderModel()
  { base.RenderModel();
    if(Mounts!=null) for(int i=0; i<Mounts.Length; i++) Mounts[i].Render(); 
  }

  public Mount[] Mounts;
}
#endregion

#region ShipClass
public sealed class ShipClass
{ ShipClass(string className)
  { XmlElement doc = Misc.LoadXml(className+".xml").DocumentElement;
    MaxAccel = Xml.Float(doc, "maxAccel");
    MaxSpeed = Xml.Float(doc, "maxSpeed")*0.1;
    TurnSpeed = Xml.Float(doc, "turnSpeed") * MathConst.DegreesToRadians;
    Name   = Xml.Attr(doc, "name");
    Model  = (ObjModel)SpaceWinds.Model.Load(Xml.Attr(doc, "model", className), doc);
    Mounts = Model.Mounts;
  }

  public double MaxSpeed, MaxAccel, TurnSpeed;
  public string Name;
  public ObjModel Model;
  public MountClass[] Mounts;
  
  public static ShipClass Load(string shipClass)
  { ShipClass ret = (ShipClass)classes[shipClass];
    if(ret==null) classes[shipClass] = ret = new ShipClass(shipClass);
    return ret;
  }
  
  static Hashtable classes = new Hashtable();
}
#endregion

#region Ship
public abstract class Ship : MountsObject
{ public void AccelerateTowards(double speed)
  { double accel=speed-Speed, max=Class.MaxAccel*Class.MaxSpeed*App.TimeDelta;
    if(Math.Abs(accel)>max) accel = max*Math.Sign(accel);
    if(accel<0) accel *= 0.5;

    Speed += accel;

    if(Speed>Class.MaxSpeed) Speed = Class.MaxSpeed;
    else
    { max = -Class.MaxSpeed*(1/3.0);
      if(Speed<max) Speed = max;
    }
  }

  public override void Update() { Pos += new Vector(Speed, 0).Rotated(Angle)*App.TimeDelta; }

  public void SetClass(string shipClass)
  { Class  = ShipClass.Load(shipClass);
    Model  = Class.Model;
    Mounts = new Mount[Class.Mounts.Length];
    for(int i=0; i<Mounts.Length; i++) Mounts[i].Class = Class.Mounts[i];
  }

  public void TurnTowards(Point pt) { TurnTowards(GLMath.AngleBetween(Pos, pt)); }
  public void TurnTowards(double desiredAngle)
  { double turn=desiredAngle-Angle, max=Class.TurnSpeed*App.TimeDelta;
    if(turn>Math.PI) turn -= Math.PI*2;
    else if(turn<-Math.PI) turn += Math.PI*2;
    if(Math.Abs(turn)>max) turn = max*Math.Sign(turn);

    Angle = Misc.NormalizeAngle(Angle+turn);
  }

  public double Throttle, Speed;
  public ShipClass Class;
}
#endregion

public class AIShip : Ship
{ public override void Update()
  { AccelerateTowards(Class.MaxSpeed);
    double angle = GLMath.AngleBetween(Pos, App.Player.Pos);
    double dist = (App.Player.Pos - Pos).Length;
    if(dist<20) TurnTowards(Angle+2);
    else TurnTowards(angle);
    AimAt(App.Player.Pos);
    base.Update();
    if(dist<50) for(int i=0; i<Mounts.Length; i++) Mounts[i].Fire(this);
  }
}

#region Player
public sealed class Player : Ship
{ public override void Update()
  { if(Mouse.PressedRel(MouseButton.Right)) turnTowardsCursor = !turnTowardsCursor;

    { Point3 pt3 = Misc.Unproject(Mouse.Point);
      Point pt = new Point(pt3.X, pt3.Y);
      if(turnTowardsCursor) TurnTowards(pt);
      AimAt(pt);
    }

    if(Keyboard.Pressed(Key.Tab))
    { double accel = Class.MaxAccel*Class.MaxSpeed*2*App.TimeDelta;
      Speed = Math.Min(Speed+accel, Class.MaxSpeed*2.5);
    }
    else
    { if(Keyboard.Pressed(Key.Q) || Keyboard.Pressed(Key.A))
      { if(Keyboard.Pressed(Key.Q)) Throttle = Math.Min(Throttle+App.TimeDelta, 1);
        else Throttle = Math.Max(Throttle-App.TimeDelta, -0.5);
      }
      if(Keyboard.PressedRel(Key.Backquote)) Throttle = 0;
      AccelerateTowards(Class.MaxSpeed*Throttle);
    }

    base.Update();

    if(Mounts!=null && Mouse.Pressed(MouseButton.Left)) for(int i=0; i<Mounts.Length; i++) Mounts[i].Fire(this);
  }
  
  bool turnTowardsCursor;
}
#endregion

} // namespace SpaceWinds