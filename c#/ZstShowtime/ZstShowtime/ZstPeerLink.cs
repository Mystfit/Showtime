using System;
using System.Collections;
using System.Collections.Generic;
using NetMQ;
using Newtonsoft.Json;

namespace ZST {
	public class ZstPeerLink {

        //Message constants
		public static string REPLY_ADDRESS = "zst_key_rep_address";
    	public static string PUBLISHER_ADDRESS = "zst_key_pub_address";
   		public static string NAME = "zst_key_name";
    	public static string METHOD_LIST = "zst_method_list";

        /// <summary>Get the name of this node</summary>
        public string name { get { return m_name; } }
        protected string m_name;

        /// <summary>Get the address of this remote node's reply socket.</summary>
        public string replyAddress { get { return m_replyAddress; } }
        protected string m_replyAddress;

        /// <summary>Get the address of this remote node's publisher socket.</summary>
        public string publisherAddress { get { return m_publisherAddress; } }
        protected string m_publisherAddress;

        /// <summary>Get the method references for this remote peer.</summary>
        public Dictionary<string, ZstMethod> methods { get { return m_methods; } }
        protected Dictionary<string, ZstMethod> m_methods;

        //Sockets
		public NetMQSocket request { 
			get{ return m_request; } 
			set{ m_request = value; }
		}
    	protected NetMQSocket m_request;

		public NetMQSocket subscriber { 
			get{ return m_subscriber; } 
			set{ m_subscriber = value; } 
		}
    	protected NetMQSocket m_subscriber;


        //Constructors
        //------------
		public ZstPeerLink(string name, string replyAddress, string publisherAddress)
        {
			init(name, replyAddress, publisherAddress, new Dictionary<string, ZstMethod>());
		}

    	public ZstPeerLink(string name, string replyAddress, string publisherAddress, Dictionary<string, ZstMethod> methods)
        {
			init(name, replyAddress, publisherAddress, methods);
		}

		private void init(string name, string replyAddress, string publisherAddress, Dictionary<string, ZstMethod> methods)
        {
			m_name = name;
			m_replyAddress = replyAddress;
			m_publisherAddress = publisherAddress;
			m_methods = methods;
		}

        /// <summary>Close all sockets connected to this peer</summary>
        public void disconnect()
        {
            if (m_request != null)
            {
                m_request.Dispose();
                m_request = null;
            }

            if (m_subscriber != null)
            {
                m_subscriber.Dispose();
                m_subscriber = null;
            }
        }


        /// <summary>
        /// Returns this class as a dictionary.
        /// </summary>
		public Dictionary<string, object> as_dict()
        {
			Dictionary<string, object> outDict = new Dictionary<string, object>();
			Dictionary<string, Dictionary<string, object> > methods = new Dictionary<string, Dictionary<string, object> >();

			foreach(KeyValuePair<string, ZstMethod> method in m_methods)
            {
				methods[method.Key] = method.Value.as_dict();
			}

			return new Dictionary<string, object>()
            {
				{NAME, m_name},
				{PUBLISHER_ADDRESS, m_publisherAddress},
				{REPLY_ADDRESS, m_replyAddress},
				{METHOD_LIST, methods}
			};
		}

        /// <summary>
        /// Build local Peerlinks from a dictionary list.
        /// </summary>
		public static Dictionary<string, ZstPeerLink> buildLocalPeerlinks(Dictionary<string, object> peers)
        {
			Dictionary<string, ZstPeerLink> outPeerLinks = new Dictionary<string, ZstPeerLink>();
			
			foreach(KeyValuePair<string, object> peer in peers){
                Dictionary<string, object> peerDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(peer.Value.ToString());

                Dictionary<string, object> inMethods = JsonConvert.DeserializeObject<Dictionary<string, object>>(peerDict[METHOD_LIST].ToString());
				Dictionary<string, ZstMethod> methodList = ZstMethod.buildLocalMethods(inMethods);
				outPeerLinks[peer.Key] = new ZstPeerLink(
                    (string)peerDict[NAME],
                    (string)peerDict[REPLY_ADDRESS],
                    (string)peerDict[PUBLISHER_ADDRESS],
					methodList);
			}

			return outPeerLinks;
		}
	}
}