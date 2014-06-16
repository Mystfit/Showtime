using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ZST;

namespace ZstTestClient
{
    class Program
    {
        static void Main(string[] args)
        {
            ZstNode node = new ZstNode("csharptest", "tcp://130.195.44.90:6000");
            node.requestRegisterNode();

            Dictionary<string, object> nodeArgs = new Dictionary<string, object>(){
                {"greeting", ""}};
            node.requestRegisterMethod("testMethod", ZstMethod.WRITE, nodeArgs, testMethod);

            Dictionary<string, ZstPeerLink> peerList = node.requestNodePeerlinks();

            Console.WriteLine("");
            foreach (KeyValuePair<string, ZstPeerLink> peer in peerList)
            {
                Console.WriteLine("Node: " + peer.Key);
                Console.WriteLine("--------------");

                foreach (KeyValuePair<string, ZstMethod> method in peer.Value.methods)
                {
                    Console.Write(method.Key);
                    if (method.Value.args.Count > 0)
                        Console.Write(" | ");
                    foreach(KeyValuePair<string, object> arg in method.Value.args)
                        Console.Write(arg.Key + ", ");
                    Console.WriteLine("");
                }
                Console.WriteLine("");  
            }


            for (int i = 0; i < 10; i++)
            {
                node.updateLocalMethodByName("testMethod", i);
            }
        }

        public static object testMethod(ZstMethod messageData)
        {
            Console.WriteLine("Hello " + messageData.args["greeting"].ToString());
            return null;
        }
    }
}
