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
using RectF=System.Drawing.RectangleF;

namespace SpaceWinds
{

public enum MountType : byte { Weapon }

// each hit type hits all the rest of the types (eg, Bullet hits Missile and Ship, and Missile only hits Ship)
// the exception is NoHit, which hits nothing
[Flags] public enum ObjFlag : byte
{ NoHit=0, Bullet=1, Missile=2, Ship=3, Planet=4, HitMask=0x07,
  Dead=8
}

#region Mount
public struct Mount
{ public void Fire(Ship owner) { if(Mounted!=null) Mounted.Fire(owner, ref this); }

  public void Render()
  { if(Mounted==null || Class.Model==null) return;
    GL.glPushMatrix();
    GL.glTranslated(Class.RenderOffset);
    GL.glRotated(MathConst.RadiansToDegrees * (Class.CenterAngle+Offset), 0, 0, 1);
    Class.Model.Render();
    GL.glPopMatrix();
  }

  public void TurnTowards(float desiredAngle)
  { if(Mounted==null) return;
    if(Class.MaxTurn==0) { Offset=0; return; }

    float turn, max;
    desiredAngle = Misc.NormalizeAngle(desiredAngle);

    if(Class.MaxTurn==Math.PI)
    { turn = desiredAngle-(Class.CenterAngle+Offset);
      if(turn>(float)Math.PI) turn -= (float)(Math.PI*2);
      else if(turn<(float)(-Math.PI)) turn += (float)(Math.PI*2);
    }
    else
    { float min = Misc.NormalizeAngle(Class.CenterAngle-Class.MaxTurn);
      max = Misc.NormalizeAngle(Class.CenterAngle+Class.MaxTurn);
      if(min<max)
      { if(desiredAngle<min) desiredAngle = min;
        else if(desiredAngle>max) desiredAngle = max;
      }
      else if(desiredAngle<min && desiredAngle>max)
        desiredAngle = Math.Abs(desiredAngle-min)<Math.Abs(desiredAngle-max) ? min : max;

      min = Class.CenterAngle + (float)Math.PI; // hijack 'min' to be the opposite of the center angle
      if(min>=(float)(Math.PI*2)) min -= (float)(Math.PI*2);

      float cur = Class.CenterAngle + Offset;
      turn = desiredAngle - cur;
      if(turn<=(float)(-Math.PI*2)) turn += (float)(Math.PI*2);
      else if(turn>=(float)(Math.PI*2)) turn -= (float)(Math.PI*2);
      if(Math.Abs(turn)<0.00001f) { return; } // prevent ugly turret jitter (due to FP inaccuracy) and also optimize the turn==0 case
      if(Class.TurnSpeed==0) goto turn;

      cur = Misc.NormalizeAngle(cur-min);
      desiredAngle = Misc.NormalizeAngle(desiredAngle-min);

      if(cur<desiredAngle)
      { if(turn<0) turn += (float)(Math.PI*2);
      }
      else if(turn>0) turn -= (float)(Math.PI*2);
    }

    max = Class.TurnSpeed*App.TimeDelta;
    if(Math.Abs(turn)>max) turn = max*Math.Sign(turn);

    turn:
    Offset += turn;
    if(Offset<(float)(-Math.PI*2)) Offset += (float)(Math.PI*2);
    else if(Offset>(float)(Math.PI*2)) Offset -= (float)(Math.PI*2);
  }

  public MountClass Class;
  public Mountable Mounted;
  public float Offset;
}
#endregion

#region MountClass
public sealed class MountClass
{ public float CenterAngle, MaxTurn, TurnSpeed;
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

  protected void GetInfo(Ship owner, ref Mount mount, out Point startPoint, out Vector baseVelocity, out float angle)
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
      float gunAngle;
      GetInfo(owner, ref mount, out startPoint, out baseVelocity, out gunAngle);
      SpaceObject proj = Class.CreateProjectile(startPoint, baseVelocity, gunAngle);
      proj.Owner = owner;
      owner.Map.Add(proj); // TODO: support non-projectile (eg laser) weapons
    }
  }

  public float LastFired;
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
{ public abstract SpaceObject CreateProjectile(Point startPoint, Vector baseVelocity, float gunAngle);
  public float ReloadTime;
  public int MaxAmmo;
}
#endregion

public sealed class SimpleGun : WeaponClass
{ public SimpleGun() { MaxAmmo=-1; ReloadTime=1; }

  public override SpaceObject CreateProjectile(Point startPoint, Vector baseVelocity, float gunAngle)
  { return new Bullet(startPoint, baseVelocity+new Vector(15, 0).Rotated(gunAngle));
  }
}

public sealed class Bullet : SpaceObject
{ public Bullet(Point pos, Vector vel)
  { X=(float)pos.X; Y=(float)pos.Y; Angle=(float)vel.Angle; Velocity=vel; Born=App.Now;
    Flags |= ObjFlag.Bullet;
  }

  public override float BiggestRadius { get { return 0.05f; } }
  public override float GetRadiusSquared(float angle) { return 0.05f; }

  public override void Hit(SpaceObject so)
  { Flags |= ObjFlag.Dead;
    so.TakeDamage(this, 30);
  }

  public override void RenderModel()
  { GL.glDisable(GL.GL_LIGHTING);
    GL.glPointSize(2);
    GL.glBegin(GL.GL_POINTS);
      GL.glColor(System.Drawing.Color.White);
      GL.glVertex2d(0, 0);
    GL.glEnd();
    GL.glEnable(GL.GL_LIGHTING);
  }

  public override void Update()
  { if(App.Now-Born>1.5) Set(ObjFlag.Dead, true);
    else
    { X += (float)(Velocity.X*App.TimeDelta);
      Y += (float)(Velocity.Y*App.TimeDelta);
    }
  }
  
  public Vector Velocity;
  public float Born;
}

#region SpaceObject
public class SpaceObject
{ public bool Is(ObjFlag flag) { return (Flags&flag)!=0; }
  public void Set(ObjFlag flag, bool on)
  { if(on) Flags |= flag;
    else Flags &= ~flag;
  }

  public virtual float BiggestRadius { get { return Model.RadiusSquared; } }

  public Point Pos
  { get { return new Point(X, Y); }
    set { X=(float)value.X; Y=(float)value.Y; }
  }

  public virtual float GetRadiusSquared(float angle) { return Model.RadiusSquared; }
  public virtual void Hit(SpaceObject so) { }

  public virtual void Render()
  { GL.glPushMatrix();
    GL.glTranslatef(X, Y, 0);
    GL.glRotated(MathConst.RadiansToDegrees * Angle, 0, 0, 1); // negate the angle because we're rotating the /camera/
    RenderModel();
    GL.glPopMatrix();
  }

  public virtual void RenderModel() { Model.Render(); }
  public virtual void TakeDamage(SpaceObject from, float amount) { }
  public virtual void Update() { }

  public Map Map;
  public Model Model;
  public SpaceObject Owner;
  public float X, Y;
  public float Angle;
  public ObjFlag Flags;
  
  public static bool Collided(SpaceObject a, SpaceObject b) // returns true if a hit b
  { if(a.Owner==b) return false;

    float radius=a.BiggestRadius+b.BiggestRadius, xd=a.X-b.X, yd=a.Y-b.Y, dist=xd*xd+yd*yd;
    if(dist>radius) return false;

    float angle = Misc.AngleBetween(a.Pos, b.Pos);
    radius = a.GetRadiusSquared(angle)+b.GetRadiusSquared(angle-(float)Math.PI);
    return dist<=radius;
  }
}
#endregion

#region Planet
public class Planet : SpaceObject
{ public Planet() { Flags |= ObjFlag.Planet; }

  public override void Render()
  { GL.glPushMatrix();
    GL.glTranslatef(X, Y, 0);
    if(AxisAngle!=0) GL.glRotated(AxisAngle*MathConst.RadiansToDegrees, Axis.X, Axis.Y, Axis.Z);
    GL.glRotated(Angle*MathConst.RadiansToDegrees, 0, 0, 1);
    RenderModel();
    GL.glPopMatrix();
  }

  public void SetAxis(GameLib.Mathematics.ThreeD.Quaternion quat)
  { double angle;
    quat.GetAxisAngle(out Axis, out angle);
    AxisAngle = (float)angle;
  }

  public override void Update() { Angle = Misc.NormalizeAngle(Angle+RotateSpeed*App.TimeDelta); }

  public Vector3 Axis;
  public float AxisAngle, RotateSpeed;
}
#endregion

#region MountsObject
public abstract class MountsObject : SpaceObject
{ public void AimAt(Point pt)
  { if(Mounts!=null)
    { Point3 opt = new Point3(X, Y, 0);
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
    MaxAccel    = Xml.Float(doc, "maxAccel");
    MaxSpeed    = Xml.Float(doc, "maxSpeed")*0.1f;
    TurnSpeed   = (float)(Xml.Float(doc, "turnSpeed") * MathConst.DegreesToRadians);
    MaxShield   = Xml.Float(doc, "maxShield");
    ShieldRegen = Xml.Float(doc, "shieldRegen");
    BoostSpeed  = Xml.Float(doc, "boostSpeed", MaxSpeed*2f);
    BoostTime   = Xml.Float(doc, "boostTime", 4);
    BoostRegen  = Xml.Float(doc, "boostRegen", BoostTime*2.5f);
    Name        = Xml.Attr(doc, "name");
    Model       = (ObjModel)SpaceWinds.Model.Load(Xml.Attr(doc, "model", className), doc);
    Mounts      = Model.Mounts;
  }

  public float MaxSpeed, MaxAccel, TurnSpeed, MaxShield, ShieldRegen, BoostRegen, BoostSpeed, BoostTime;
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
{ public Ship() { Flags |= ObjFlag.Ship; }

  public const float ShieldThickness=0.5f, ShieldThicknessSqr=0.25f; // shield thickness at full strength (in world units)

  public override float BiggestRadius { get { return Model.RadiusSquared + ShieldThicknessSqr; } }

  #region Shields
  public struct Shields
  { public Shields(float max) { MaxStrength = max; Front = Left = Right = Rear = new Shield(max); }

    public struct Shield
    { public Shield(float max) { Strength=max; Show=0; }
      public float Strength, Show;
    }

    public float AlterStrength(int index, float amount)
    { switch(index)
      { case 0: return AlterStrength(ref Front, amount);
        case 1: return AlterStrength(ref Right, amount);
        case 2: return AlterStrength(ref Rear, amount);
        case 3: return AlterStrength(ref Left, amount);
        default: throw new IndexOutOfRangeException();
      }
    }

    public int GetIndex(float angle)
    { angle = Misc.NormalizeAngle(angle);
      if(angle<(float)(Math.PI/4)) return 0;
      if(angle<(float)(Math.PI*3/4)) return 1;
      if(angle<(float)(Math.PI*5/4)) return 2;
      if(angle<(float)(Math.PI*7/4)) return 3;
      return 0;
    }

    public float GetShow(int index)
    { switch(index)
      { case 0: return Front.Show;
        case 1: return Right.Show;
        case 2: return Rear.Show;
        case 3: return Left.Show;
        default: throw new IndexOutOfRangeException();
      }
    }

    public float GetStrength(int index)
    { switch(index)
      { case 0: return Front.Strength;
        case 1: return Right.Strength;
        case 2: return Rear.Strength;
        case 3: return Left.Strength;
        default: throw new IndexOutOfRangeException();
      }
    }

    public void SetShow(int index)
    { switch(index)
      { case 0: Front.Show = 1; break;
        case 1: Right.Show = 1; break;
        case 2: Rear.Show = 1; break;
        case 3: Left.Show = 1; break;
        default: throw new IndexOutOfRangeException();
      }
    }

    public void Update(float regen)
    { regen *= App.TimeDelta;
      Update(ref Front, regen);
      Update(ref Left, regen);
      Update(ref Right, regen);
      Update(ref Rear, regen);
    }

    public Shield Front, Left, Right, Rear;
    public float MaxStrength;

    float AlterStrength(ref Shield shield, float amount)
    { shield.Strength += amount;
      if(shield.Strength<0) { amount=shield.Strength; shield.Strength=0; }
      else if(shield.Strength>MaxStrength) { amount=MaxStrength-shield.Strength; shield.Strength=MaxStrength; }
      else return 0;
      return amount;
    }

    void Update(ref Shield shield, float regen)
    { shield.Strength = Math.Min(MaxStrength, shield.Strength+regen);
      shield.Show = Math.Max(0, shield.Show-App.TimeDelta);
    }
  }
  #endregion

  public void AccelerateTowards(float speed)
  { if(speed==Speed) return;

    float accel, max;
    if(speed>Class.MaxSpeed) speed = Class.MaxSpeed;
    else
    { max = -Class.MaxSpeed*(1/3f);
      if(speed<max) speed = max;
    }

    max=speed-Speed; accel=Class.MaxSpeed*App.TimeDelta/Class.MaxAccel;
    if(max<0)
    { accel *= 0.5f; // slowing down isn't as quick as speeding up
      max = -max;
      accel = -accel;
    }
    if(Math.Abs(accel)>max) accel = max*Math.Sign(accel);

    Speed += accel;
  }

  public void BoostSpeed()
  { if(Speed<=Class.BoostSpeed)
    { float time = BoostTime;
      if(time!=0)
      { time  = Math.Min(time, App.TimeDelta);
        float accel = Class.BoostSpeed*time/Class.MaxAccel;
        Speed = Math.Min(Speed+accel, Class.BoostSpeed);
        BoostTime -= time;
      }
      if(time<App.TimeDelta)
      { time = App.TimeDelta-time;
        Speed += (Throttle*Class.MaxSpeed-Speed)*time/Class.MaxAccel;
      }
    }
  }

  public override float GetRadiusSquared(float angle)
  { return Model.RadiusSquared + Shield.GetStrength(Shield.GetIndex(angle-Angle))/Class.MaxShield*ShieldThicknessSqr;
  }

  public override void RenderModel()
  { base.RenderModel();

    if(Shield.Front.Show!=0 || Shield.Right.Show!=0 || Shield.Rear.Show!=0 || Shield.Left.Show!=0)
    { float radius = (float)Math.Sqrt(Model.RadiusSquared);
      GL.glDisable(GL.GL_LIGHTING);
      GL.glEnable(GL.GL_BLEND);
      if(Shield.Front.Show!=0) RenderShield(ref Shield.Front, radius);
      if(Shield.Right.Show!=0) RenderShield(ref Shield.Right, radius, 90);
      if(Shield.Rear.Show!=0) RenderShield(ref Shield.Rear, radius, 180);
      if(Shield.Left.Show!=0) RenderShield(ref Shield.Left, radius, 270);
      GL.glEnable(GL.GL_LIGHTING);
      GL.glDisable(GL.GL_BLEND);
    }
  }

  public void SetClass(string shipClass)
  { Class  = ShipClass.Load(shipClass);
    Model  = Class.Model;
    Mounts = new Mount[Class.Mounts.Length];
    Shield = new Shields(Class.MaxShield);
    BoostTime = Class.BoostTime;
    for(int i=0; i<Mounts.Length; i++) Mounts[i].Class = Class.Mounts[i];
  }

  public override void TakeDamage(SpaceObject from, float amount)
  { int index = Shield.GetIndex(Misc.AngleBetween(Pos, from.Pos)-Angle);
    amount = -Shield.AlterStrength(index, -amount);
    Shield.SetShow(index);
    
    Health -= amount;
    if(Health<=0) Flags |= ObjFlag.Dead;
  }

  public void TurnTowards(Point pt) { TurnTowards(Misc.AngleBetween(Pos, pt)); }
  public void TurnTowards(float desiredAngle)
  { float turn=desiredAngle-Angle, max=Class.TurnSpeed*App.TimeDelta;
    if(turn>(float)Math.PI) turn -= (float)(Math.PI*2);
    else if(turn<(float)(-Math.PI)) turn += (float)(Math.PI*2);
    if(Math.Abs(turn)>max) turn = max*Math.Sign(turn);

    Angle = Misc.NormalizeAngle(Angle+turn);
  }

  public override void Update()
  { Shield.Update(Class.ShieldRegen);
    Vector vel = new Vector(Speed, 0).Rotated(Angle)*App.TimeDelta;
    X += (float)vel.X; Y += (float)vel.Y;
  }

  public Shields Shield;
  public float Throttle, Speed, BoostTime, Health=80;
  public ShipClass Class;
  
  void RenderShield(ref Shields.Shield shield, float radius, float rotate)
  { GL.glPushMatrix();
    GL.glRotatef(rotate, 0, 0, 1);
    RenderShield(ref shield, radius);
    GL.glPopMatrix();
  }
  
  void RenderShield(ref Shields.Shield shield, float radius)
  { float inside = radius*0.8f;
    float factor = shield.Strength/Class.MaxShield*shield.Show, alpha1 = 0.75f*factor, alpha2 = 0.25f*factor;
    GL.glBegin(GL.GL_TRIANGLES);
      GL.glColor4d(0, 80/255.0, 160/255.0, alpha1);
      GL.glVertex3f(radius, -radius, Model.MinZ);
      GL.glVertex3f(radius, radius, Model.MinZ);
      GL.glColor4d(0, 80/255.0, 160/255.0, alpha2);
      GL.glVertex3d(radius+ShieldThickness, inside, Model.MinZ);
      GL.glVertex3d(radius+ShieldThickness, inside, Model.MinZ);
      GL.glVertex3d(radius+ShieldThickness, -inside, Model.MinZ);
      GL.glColor4d(0, 80/255.0, 160/255.0, alpha1);
      GL.glVertex3d(radius, -radius, Model.MinZ);
    GL.glEnd();
  }
}
#endregion

public class AIShip : Ship
{ public override void Update()
  { AccelerateTowards(Class.MaxSpeed);
    if(App.Player.Is(ObjFlag.Dead)) base.Update();
    else
    { float angle = Misc.AngleBetween(Pos, App.Player.Pos);
      float dist = (float)(App.Player.Pos - Pos).Length;
      if(dist<5) TurnTowards(Angle+2);
      else TurnTowards(angle);
      AimAt(App.Player.Pos);
      base.Update();
      if(dist<10) for(int i=0; i<Mounts.Length; i++) Mounts[i].Fire(this);
    }
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

    if(Keyboard.Pressed(Key.Q) || Keyboard.Pressed(Key.A))
    { if(Keyboard.Pressed(Key.Q)) Throttle = Math.Min(Throttle+App.TimeDelta, 1);
      else Throttle = Math.Max(Throttle-App.TimeDelta, -0.5f);
    }
    if(Keyboard.PressedRel(Key.Backquote)) Throttle = Throttle==0 ? 1 : 0;

    if(Keyboard.Pressed(Key.Tab)) BoostSpeed();
    else
    { AccelerateTowards(Class.MaxSpeed*Throttle);
      if(BoostTime<Class.BoostTime)
        BoostTime = Math.Min(BoostTime+Class.BoostTime/Class.BoostRegen*App.TimeDelta, Class.BoostTime);
    }

    base.Update();

    if(Mounts!=null && Mouse.Pressed(MouseButton.Left)) for(int i=0; i<Mounts.Length; i++) Mounts[i].Fire(this);
  }
  
  bool turnTowardsCursor;
}
#endregion

} // namespace SpaceWinds