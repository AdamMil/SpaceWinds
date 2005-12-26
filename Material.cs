using System;
using System.Collections;
using System.IO;
using System.Text.RegularExpressions;
using GameLib.Interop.OpenGL;

namespace SpaceWinds
{

public abstract class Material
{ public string Name { get { return name; } }

  public static Material Current
  { get { return current; }
    set
    { if(value==null) throw new NullReferenceException();
      if(current!=value)
      { value.Apply();
        current = value;
      }
    }
  }

  public static Material Get(string name) { return (Material)materials[name]; }

  protected abstract void Apply();
  protected string name;

  protected static Hashtable materials = new Hashtable();
  static Material current;
}

public sealed class ObjMaterial : Material
{ ObjMaterial(TextReader tr, ref string nameLine)
  { name      = nameLine.Substring(7);
    Alpha     = 1;
    Ambient   = new Color(0.2f, 0.2f, 0.2f);
    Diffuse   = new Color(0.8f, 0.8f, 0.8f);
    Specular  = new Color(1, 1, 1);
    Model     = 1;
    
    while(true)
    { string line = tr.ReadLine();
      if(line==null || line.StartsWith("newmtl ")) { nameLine=line; break; }
      if(line.StartsWith("Ka ")) Ambient = GetColor(line);
      else if(line.StartsWith("Kd ")) Diffuse = GetColor(line);
      else if(line.StartsWith("Ks ")) Specular = GetColor(line);
      else if(line.StartsWith("d ") || line.StartsWith("Tr ")) Alpha = GetValue(line);
      else if(line.StartsWith("Ns ")) Shininess = GetValue(line)*(128f/1000f);
      else if(line.StartsWith("illum")) Model = (int)GetValue(line);
    }
  }
  
  public struct Color
  { public Color(float r, float g, float b) { R=r; G=g; B=b; }
    public float R, G, B;
  }

  public readonly Color Ambient, Diffuse, Emit, Specular;
  public readonly float Alpha, Shininess;
  public readonly int Model;

  public static void LoadLibrary(string path)
  { if(loaded==null) { loaded = new ArrayList(); loaded.Add(path); }
    else if(loaded.Contains(path)) return;

    TextReader tr = new StreamReader(App.DataPath+path);
    string line = tr.ReadLine();
    while(true)
    { if(line==null) break;
      if(line.StartsWith("newmtl "))
      { ObjMaterial m = new ObjMaterial(tr, ref line);
        materials[m.Name] = m;
      }
      else line = tr.ReadLine();
    }
    tr.Close();
  }

  protected override void Apply()
  { if(Alpha==1) GL.glDisable(GL.GL_BLEND);
    else GL.glEnable(GL.GL_BLEND);

    if(Model==0) GL.glMaterialColor(GL.GL_FRONT, GL.GL_AMBIENT, Diffuse.R, Diffuse.G, Diffuse.B, Alpha);
    else GL.glMaterialColor(GL.GL_FRONT, GL.GL_AMBIENT, Ambient.R, Ambient.G, Ambient.B, Alpha);

    GL.glMaterialColor(GL.GL_FRONT, GL.GL_DIFFUSE, Diffuse.R, Diffuse.G, Diffuse.B, Alpha);
    GL.glMaterialColor(GL.GL_FRONT, GL.GL_EMISSION, Emit.R, Emit.G, Emit.B, Alpha);

    if(Model==2)
    { GL.glMaterialColor(GL.GL_FRONT, GL.GL_SPECULAR, Specular.R, Specular.G, Specular.B, 1);
      GL.glMaterialf(GL.GL_FRONT, GL.GL_SHININESS, Shininess);
    }
    else
    { GL.glMaterialColor(GL.GL_FRONT, GL.GL_SPECULAR, 0, 0, 0, 1);
      GL.glMaterialf(GL.GL_FRONT, GL.GL_SHININESS, 0);
    }
  }

  static Color GetColor(string line)
  { string[] c = line.Split(' ');
    if(c.Length==4) return new Color(float.Parse(c[1]), float.Parse(c[2]), float.Parse(c[3]));
    else
    { float v = float.Parse(c[1]);
      return new Color(v, v, v);
    }
  }

  static float GetValue(string line) { return float.Parse(line.Substring(line.IndexOf(' ')+1)); }
  
  static ArrayList loaded;
}

} // namespace SpaceWinds