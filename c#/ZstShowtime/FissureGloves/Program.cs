using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ZST;
using MathGeom;
using Newtonsoft.Json;

namespace FissureGloves
{
    class Program
    {
        static void Main(string[] args)
        {
            SixenseInput input = new SixenseInput();
            input.Init();
            input.RebindHands();
            Console.WriteLine("Gloves activated");

            ZstNode node = new ZstNode("FissureGloves", "tcp://130.195.44.90:6000");
            node.requestRegisterNode();
            node.requestRegisterMethod("glove_update", ZstMethod.READ);


            Console.WriteLine("Engage!");

            while (true)
            {
                input.Update();

                ZstMethod transformUpdate = node.methods["glove_update"].clone();
                Dictionary<int, object> gloveData = new Dictionary<int, object>();

                for (int i = 1; i <= 2; i++)
                {
                    Vector3 pos = SixenseInput.GetController ((SixenseHands)i).Position;
                    Quaternion rot = SixenseInput.GetController((SixenseHands)i).Rotation;

                    float[] transform = new float[]{ pos.x, pos.y, pos.z, rot.x, rot.y, rot.z, rot.w };
                    gloveData[i-1] = transform;
                    node.updateLocalMethod(transformUpdate, gloveData);
                }
                Thread.Sleep(10);
            }
        }
    }
}
