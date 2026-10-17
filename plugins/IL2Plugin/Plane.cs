using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace YawVR_Game_Engine.Plugin
{
    public class Plane
    {
        public Vector3 m_v3Position = new Vector3();
        public Vector3 m_v3Normal = new Vector3();

        public Plane(Vector3 v3Position, Vector3 v3Normal)
        {
            m_v3Position = new Vector3(v3Position.X, v3Position.Y, v3Position.Z);
            m_v3Normal = new Vector3(v3Normal.X, v3Normal.Y, v3Normal.Z);
        }

        public float GetDistance(Vector3 v3Point)
        {
            float t = Vector3.Dot(m_v3Normal, v3Point - m_v3Position);
            return t;
        }
    }
}
