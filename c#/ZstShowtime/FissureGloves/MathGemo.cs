using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MathGeom
{
    public class Vector3
    {
        public Vector3(float x, float y, float z)
        {
            Set(x, y, z);
        }
        protected float m_x;
        protected float m_y;
        protected float m_z;

        public float x { get { return m_x; } set { m_x = value; } }
        public float y { get { return m_y; } set { m_y = value; } }
        public float z { get { return m_z; } set { m_z = value; } }

        public void Set(float x, float y, float z)
        {
            m_x = x;
            m_y = y;
            m_z = z;
        }

        public override string ToString()
        {
            return m_x.ToString() + ", " + m_y.ToString() + ", " + m_z.ToString();
        }

        public float[] asFloat{ get { return new float[] { x, y, z}; } }


    }

    public class Quaternion
    {
        public Quaternion(float x, float y, float z, float w)
        {
            Set(x, y, z, w);
        }
        protected float m_x;
        protected float m_y;
        protected float m_z;
        protected float m_w;

        public float x { get { return m_x; } set { m_x = value; } }
        public float y { get { return m_y; } set { m_y = value; } }
        public float z { get { return m_z; } set { m_z = value; } }
        public float w { get { return m_w; } set { m_w = value; } }

        public void Set(float x, float y, float z, float w)
        {
            m_x = x;
            m_y = y;
            m_z = z;
            m_w = w;
        }

        public override string ToString()
        {
            return m_x.ToString() + ", " + m_y.ToString() + ", " + m_z.ToString() + ", " + m_w.ToString();
        }

        public float[] asFloat{ get { return new float[] { x, y, z, w}; } }
    }
}
