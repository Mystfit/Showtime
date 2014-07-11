using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ZST;

namespace ZstGetSongLayout
{
    class Program
    {
        static void Main(string[] args)
        {
            ConsoleKeyInfo cki;
            // Prevent example from ending if CTL+C is pressed.
            Console.TreatControlCAsInput = true;

            ZstNode node = new ZstNode("SongGetter", "tcp://130.195.44.51:6000");
            
            Dictionary<string, ZstPeerLink> peerList = node.requestNodePeerlinks();
            ZstPeerLink live = peerList["LiveNode"];

            node.subscribeToNode(live);
            node.connectToPeer(live);

            object output = node.updateRemoteMethod(live.methods["get_song_layout"]).output;
            Console.WriteLine(output);
            
            //do
            //{
            //    cki = Console.ReadKey();
            //    Console.WriteLine("Looping");
            //} while (cki.Key != ConsoleKey.Escape);

            node.close();
        }
    }
}
