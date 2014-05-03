using Newtonsoft.Json;
using NetMQ;
using NetMQ.Sockets;
using System;
using System.Net;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

namespace ZST
{
    public class ZstNode
    {
        // Constants
        // ---------

        // Replies
        public static string REPLY = "zst_reply";
        public static string OK = "zst_ok";

        // Methods
        public static string PUBLISH_METHODS_CHANGED = "zst_act_announce_method_change";
        public static string REPLY_REGISTER_METHOD = "reply_register_method";
        public static string REPLY_REGISTER_NODE = "reply_register_node";
        public static string REPLY_NODE_PEERLINKS = "reply_node_peerlinks";
        public static string REPLY_METHOD_LIST = "reply_list_methods";
        public static string REPLY_ALL_PEER_METHODS = "reply_all_peer_methods";
        public static string DISCONNECT_PEER = "disconnect_peer";


        // Member variables
        // ----------------
        protected string m_nodeId;
        
        protected Dictionary<string, ZstMethod> m_internalNodeMethods;
        protected string m_stageAddress;
        protected string m_replyAddress;
        protected string m_publisherAddress;

        public Dictionary<string, ZstMethod> methods { get { return m_methods; } }
        protected Dictionary<string, ZstMethod> m_methods;
        public Dictionary<string, ZstPeerLink> peers { get { return m_peers; } }
        protected Dictionary<string, ZstPeerLink> m_peers;


        // Zmq variables
        protected NetMQContext m_ctx;
        protected ResponseSocket m_reply;
        protected PublisherSocket m_publisher;
        protected SubscriberSocket m_subscriber;
        protected RequestSocket m_stage;
        protected ZstPoller m_pollerThread;


        // Constructors
        // ------------
        public ZstNode(string nodeId)
        {
            init(nodeId, "");
        }
        public ZstNode(string nodeId, string stageAddress)
        {
            init(nodeId, stageAddress);
        }

        public void init(string nodeId, string stageAddress)
        {
            m_nodeId = nodeId;
            m_methods = new Dictionary<string, ZstMethod>();
            m_internalNodeMethods = new Dictionary<string, ZstMethod>();
            m_peers = new Dictionary<string, ZstPeerLink>();
            m_stageAddress = stageAddress;
            
            //Register internal methods to the callback dictionary
            registerInternalMethods();

            m_ctx = NetMQContext.Create();
            m_reply = m_ctx.CreateResponseSocket();
			m_reply.Options.Linger = System.TimeSpan.Zero;
            m_reply.Options.ReceiveTimeout = System.TimeSpan.FromSeconds(2);

            m_publisher = m_ctx.CreatePublisherSocket();
			m_publisher.Options.Linger = System.TimeSpan.Zero;

			m_subscriber = m_ctx.CreateSubscriberSocket();
			m_subscriber.Options.Linger = System.TimeSpan.Zero;

            // Binding ports
            IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
            string hostIP = "127.0.0.1";
            foreach (IPAddress ip in host.AddressList)
            {
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    hostIP = ip.ToString();
                    break;
                }
            }

            string address = "tcp://" + hostIP;
            int port = m_reply.BindRandomPort(address);
            m_replyAddress = address + ":" + port;

            port = m_publisher.BindRandomPort(address);
            m_publisherAddress = address + ":" + port;

            // Connect to stage
            if (!string.IsNullOrEmpty(stageAddress))
            {
                m_stage = m_ctx.CreateRequestSocket();
				m_stage.Options.Linger = System.TimeSpan.Zero;
                m_stage.Connect(m_stageAddress);
                Console.WriteLine("Stage located at " + m_stage.Options.GetLastEndpoint);
                Console.WriteLine("Node reply on address " + m_replyAddress);
                Console.WriteLine("Node publisher on address " + m_publisherAddress);
            }

            // Subscribe to all incoming messages
            m_subscriber.SubscribeToAll();

            // Bind event listeners to sockets
            m_reply.ReceiveReady += receiveMethodUpdate;
            m_subscriber.ReceiveReady += receiveMethodUpdate;

            //Register application exit event
            //Application.ApplicationExit += new EventHandler(this.OnApplicationExit);

            // Intialize Poller
            m_pollerThread = new ZstPoller();
            m_pollerThread.AddSocket(m_subscriber);
            m_pollerThread.AddSocket(m_reply);
            m_pollerThread.Start();
        }

        private void registerInternalMethods()
        {
            m_internalNodeMethods[REPLY_REGISTER_NODE] = new ZstMethod(REPLY_REGISTER_NODE, m_nodeId, ZstMethod.READ, null, replyRegisterNode);
            m_internalNodeMethods[REPLY_REGISTER_METHOD] = new ZstMethod(REPLY_REGISTER_METHOD, m_nodeId, ZstMethod.READ, null, replyRegisterMethod);
            m_internalNodeMethods[REPLY_NODE_PEERLINKS] = new ZstMethod(REPLY_NODE_PEERLINKS, m_nodeId, ZstMethod.READ, null, replyNodePeerlinks);
            m_internalNodeMethods[REPLY_METHOD_LIST] = new ZstMethod(REPLY_METHOD_LIST, m_nodeId, ZstMethod.READ, null, replyRegisterNode);
            m_internalNodeMethods[REPLY_ALL_PEER_METHODS] = new ZstMethod(REPLY_ALL_PEER_METHODS, m_nodeId, ZstMethod.READ, null, replyRegisterNode);
            m_internalNodeMethods[DISCONNECT_PEER] = new ZstMethod(DISCONNECT_PEER, m_nodeId, ZstMethod.READ, null, disconnectPeer);
        }

        /// <summary>Application exiting event handler</summary>
        private void OnApplicationExit(object sender, EventArgs e)
        {
            close();
        }

        /// <summary>Close and dispose of all sockets/pollers/threads</summary>
        public bool close()
        {
			//Announce that we're leaving to all connected peers
            ZstIo.send(m_publisher, DISCONNECT_PEER, new ZstMethod(DISCONNECT_PEER, m_nodeId));

			//Clear stage
            if (m_stage != null) 
                m_stage.Dispose();

			//Disconnect all peers
			foreach(KeyValuePair<string, ZstPeerLink> peer in m_peers)
				peer.Value.disconnect();
			m_peers.Clear();

            //Exit poller
            m_pollerThread.IsDone = true;
            m_pollerThread.Update();
			m_pollerThread.Abort();
			m_pollerThread = null;

			//Clear publisher
            m_publisher.Dispose();
			m_subscriber.Dispose();
			m_reply.Dispose();

			//Clear context
            m_ctx.Dispose();
            return true;
        }

        /// <summary>Called by a peer requesting we close our connection to it</summary>
        protected object disconnectPeer(ZstMethod methodData)
        {
            Console.WriteLine("Peer '" + methodData.node + "' is leaving.");
            if (m_peers.Keys.Contains(methodData.node))
            {
                try{
                    m_subscriber.Disconnect(m_peers[methodData.node].publisherAddress);
                } catch(NetMQException e){
                    throw e;
                }

                m_peers[methodData.node].disconnect();
                m_peers.Remove(methodData.node);
            }
            return null;
        }

        /// <summary>Method update handler</summary>
        protected void receiveMethodUpdate(object sender, NetMQSocketEventArgs e)
        {
            if (e.ReceiveReady)
            {
                MethodMessage msg = ZstIo.recv(e.Socket);
                Console.Write("Recieved method '" + msg.method);
                if (msg.data != null)
                {
                    if (msg.data.output != null)
                        Console.Write("' from '" + msg.data.node + "' with value '" + msg.data.output.ToString() + "'");
                    Console.WriteLine("");
                }

                //Run local methods if we receive an update remotely to do so
                if (m_methods.Keys.Contains(msg.method))
                    m_methods[msg.method].run(msg.data);
                else if (m_internalNodeMethods.Keys.Contains(msg.method))
                    m_internalNodeMethods[msg.method].run(msg.data);

                //Run local callbacks if they're set to run when the remote method updates
                if (m_peers.Keys.Contains(msg.data.node))
                {
                    if (m_peers[msg.data.node].methods.Keys.Contains(msg.method))
                    {
                        if (m_peers[msg.data.node].methods[msg.method].callback != null) {
                            m_peers[msg.data.node].methods[msg.method].callback(msg.data);
                        } 
                    }
                }
            }
        }


        // Node registration
        //------------------
        public bool requestRegisterNode(){
            return requestRegisterNode(m_stage);
        }

        /// <summary>Request this node to be registered on another remote node</summary>
        public bool requestRegisterNode(NetMQSocket socket)
        {
            Console.WriteLine("REQ-->: Requesting remote node to register our addresses. Reply:" + m_replyAddress + ", Publisher:" + m_publisherAddress);
            
            Dictionary<string, object> requestArgs = new Dictionary<string, object>(){
                {ZstPeerLink.REPLY_ADDRESS, m_replyAddress},
                {ZstPeerLink.PUBLISHER_ADDRESS, m_publisherAddress}};
            ZstMethod request = new ZstMethod(REPLY_REGISTER_NODE, m_nodeId, "", requestArgs);

            ZstIo.send(socket, REPLY_REGISTER_NODE, request); 
            MethodMessage msg = ZstIo.recv(socket);

            if (msg.method == OK){
                Console.WriteLine("REP<--: Remote node acknowledged our addresses. Reply:" + m_replyAddress + ", Publisher:" + m_publisherAddress);
				return true;
			} else { 
                Console.WriteLine("REP<--:Remote node returned " + msg.method + " instead of " + OK);
			}
			return false;
        }

        /// <summary>Reply to another node's request for registration</summary>
        protected object replyRegisterNode(ZstMethod methodData)
        {
            if (m_peers.Keys.Contains((string)methodData.node))
                Console.WriteLine("'" + m_nodeId + "' already registered. Overwriting");

            string nodeId = (string)methodData.node;
            m_peers[nodeId] = new ZstPeerLink(
                nodeId,
                (string)methodData.args[ZstPeerLink.REPLY_ADDRESS],
                (string)methodData.args[ZstPeerLink.PUBLISHER_ADDRESS]);

            subscribeToNode(m_peers[nodeId]);
            ZstIo.send(m_reply, OK);
            Console.WriteLine("Registered node '" + nodeId + "'. Reply:" + m_peers[nodeId].replyAddress + ", Publisher:" + m_peers[nodeId].publisherAddress);
            return null;
        }

        /// <summary> Subscribe to messages coming from an external node</summary>
        public void subscribeToNode(ZstPeerLink peer)
        {
            m_subscriber.Connect(peer.publisherAddress);
            m_subscriber.SubscribeToAll();
            m_peers[peer.name] = peer;

            Console.WriteLine("Connected to peer on " + peer.publisherAddress);
        }

        /// <summary> Requests a remote node to subscribe to our requests</summary>
        public void connectToPeer(ZstPeerLink peer)
        {
            NetMQSocket socket = m_ctx.CreateRequestSocket();
			socket.Options.Linger = System.TimeSpan.Zero;
            socket.Options.ReceiveTimeout = System.TimeSpan.FromSeconds(2);

            socket.Connect(peer.replyAddress);
            
			if(requestRegisterNode(socket)){
				if(!m_peers.Keys.Contains(peer.name))
					m_peers[peer.name] = peer;
				m_peers[peer.name].request = socket;
			}
        }


        //Remote method registration
        //------------------------------------
        /// <summary>Registers a local method on a remote node</summary>
        public void requestRegisterMethod(string method, string accessMode){
            requestRegisterMethod(method, accessMode, null, null, m_stage);
        }

        /// <summary>Registers a local method on a remote node</summary>
        public void requestRegisterMethod(string method, string accessMode, Dictionary<string, object> args){
            requestRegisterMethod(method, accessMode, args, null, m_stage);
        }

        /// <summary>Registers a local method on a remote node</summary>
        public void requestRegisterMethod(string method, string accessMode, string[] args){
            Dictionary<string, object> argsDict = new Dictionary<string, object>();
            foreach (string arg in args)
                argsDict[arg] = "";
            requestRegisterMethod(method, accessMode, argsDict, null, m_stage);
        }

        /// <summary>Registers a local method on a remote node</summary>
        public void requestRegisterMethod(string method, string accessMode, Dictionary<string, object> args, NetMQSocket socket){
            requestRegisterMethod(method, accessMode, args, null, socket);
        }

        /// <summary>Registers a local method on a remote node</summary>
        public void requestRegisterMethod(string method, string accessMode, Dictionary<string, object> args, Func<ZstMethod, object> callback){
            requestRegisterMethod(method, accessMode, args, callback, m_stage);
        }

        /// <summary>Registers a local method on a remote node</summary>
        public void requestRegisterMethod(string method, string accessMode, Dictionary<string, object> args, Func<ZstMethod, object> callback, NetMQSocket socket)
        {
            Console.WriteLine("REQ-->: Registering method " + method + " with remote node.");

            //Register local copy of our method first
            m_methods[method] = new ZstMethod(method, m_nodeId, accessMode, args, callback);

            //Register method copy on remote node
            ZstIo.send(socket, REPLY_REGISTER_METHOD, m_methods[method]);
            MethodMessage msg = ZstIo.recv(socket);

            if (msg.method == OK)
                Console.WriteLine("REP<--: Remote node acknowledged our method '" + method + "'");
            else
                Console.WriteLine("REP<--:Remote node returned " + msg.method + " instead of " + OK);
        }

        /// <summary>Reply to another node's method registration request</summary>
        protected object replyRegisterMethod(ZstMethod methodData)
        {
            if (m_peers[methodData.node].methods.Keys.Contains(methodData.name))
                Console.WriteLine("'" + methodData.name + "' already registered on node " + methodData.node + ". Overwriting");

            ZstMethod localMethod = new ZstMethod(
                methodData.name,
                methodData.node,
                methodData.accessMode,
                methodData.args
            );

            m_peers[methodData.node].methods[methodData.name] = localMethod;

            ZstIo.send(m_reply, OK);
            Console.WriteLine("Registered method '" + localMethod.name + "'. Origin:" + localMethod.node + ", AccessMode:" + localMethod.accessMode + ", Args:" + localMethod.args);
            return null;
        }


        // Node peerlink accessors
        //------------------------
        /// <summary>Request a dictionary of peers nodes linked to the remote node</summary>
        public Dictionary<string, ZstPeerLink> requestNodePeerlinks()
        {
            return requestNodePeerlinks(m_stage);
        }

        /// <summary>Request a dictionary of peers nodes linked to the remote node</summary>
        public Dictionary<string, ZstPeerLink> requestNodePeerlinks(NetMQSocket socket)
        {
            ZstIo.send(socket, REPLY_NODE_PEERLINKS);
            Dictionary<string, object> peerDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(ZstIo.recv(socket).data.output.ToString());
            return ZstPeerLink.buildLocalPeerlinks(peerDict);
        }

        /// <summary>Reply to another node's request for this node's linked peers</summary>
        protected object replyNodePeerlinks(ZstMethod methodData)
        {
            Dictionary<string, object> peerDict = new Dictionary<string, object>();
            foreach (KeyValuePair<string, ZstPeerLink> peer in m_peers)
                peerDict[peer.Key] = peer.Value.as_dict();
            ZstMethod request = new ZstMethod(REPLY_NODE_PEERLINKS, m_nodeId, "", null);
            request.output = peerDict;
            ZstIo.send(m_reply, OK, request);
            return null;
        }

        public Dictionary<string, ZstMethod> requestMethodList()
        {
            return requestMethodList(m_stage);
        }


        // Node method accessors
        //----------------------
        /// <summary>Request a dictionary of methods on a remote node</summary>
        public Dictionary<string, ZstMethod> requestMethodList(NetMQSocket socket)
        {
            ZstIo.send(socket, REPLY_METHOD_LIST);
            return ZstMethod.buildLocalMethods((Dictionary<string, object>)ZstIo.recv(socket).data.output);
        }

        /// <summary>Reply with a list of methods this node owns</summary>
        public object replyMethodList(ZstMethod methodData)
        {
            Dictionary<string, object> methodDict = new Dictionary<string, object>();
            foreach (KeyValuePair<string, ZstMethod> method in m_methods)
                methodDict[method.Key] = method.Value.as_dict();
            ZstMethod request = new ZstMethod(REPLY_NODE_PEERLINKS, m_nodeId, "", null);
            request.output = methodDict;
            ZstIo.send(m_reply, OK, request);
            return null;
        }


        // Get all methods on all connected peers
        //----------------------
        /// <summary>Request a dictionary of methods on a remote node</summary>
        public Dictionary<string, ZstMethod> requestAllPeerMethods()
        {
            return requestAllPeerMethods(m_stage);
        }

        /// <summary>Request a list of all available methods provided by all connected peers on the remote node</summary>
        public Dictionary<string, ZstMethod> requestAllPeerMethods(NetMQSocket socket)
        {
            ZstIo.send(socket, REPLY_METHOD_LIST);
            return ZstMethod.buildLocalMethods((Dictionary<string, object>)ZstIo.recv(socket).data.output);
        }

        /// <summary>Reply with a list of all available methods provided by all connected peers on the remote node</summary>
        public object replyAllPeerMethods(ZstMethod methodData)
        {
            Dictionary<string, object> methodDict = new Dictionary<string, object>();
            foreach (KeyValuePair<string, ZstPeerLink> peer in m_peers)
            {
                foreach (KeyValuePair<string, ZstMethod> method in peer.Value.methods)
                    methodDict[method.Key] = method.Value.as_dict();
            }
            ZstMethod request = new ZstMethod(REPLY_NODE_PEERLINKS, m_nodeId, "", null);
            request.output = methodDict;
            ZstIo.send(m_reply, OK, request);
            return null;
        }

        
        // Method publishing / controlling
        // -------------------------------
        /// <summary>Updates the value of a local variable by name</summary>
        public void updateLocalMethodByName(string method, object methodvalue)
        {
            if (m_methods.Keys.Contains(method))
                updateLocalMethod(m_methods[method], methodvalue);
        }

        /// <summary>Updates the value of a local variable</summary>
        public void updateLocalMethod(ZstMethod method, object value)
        {
            if (method.node == m_nodeId)
            {
                NetMQSocket socket;
                if (method.accessMode == ZstMethod.RESPONDER)
                    socket = m_reply;
                else
                    socket = m_publisher;

                method.output = value;
                ZstIo.send(socket, method.name, method);
            }
        }

        /// <summary>Triggers a remote method with arguments</summary>
        public ZstMethod updateRemoteMethodByName(string name)
        {
            return updateRemoteMethodByName(name, null); 
        }

        /// <summary>Triggers a remote method with arguments</summary>
        public ZstMethod updateRemoteMethodByName(string name, Dictionary<string, object> args)
        {   
            if(m_methods.Keys.Contains(name)){
                ZstMethod method = m_methods[name];
                return updateRemoteMethod(method, args);
            }
            return null;
        }

        /// <summary>Triggers a remote method with arguments</summary>
        public ZstMethod updateRemoteMethod(ZstMethod method)
        {
            return updateRemoteMethod(method, null);
        }

        /// <summary>Triggers a remote method with arguments</summary>
        public ZstMethod updateRemoteMethod(ZstMethod method, Dictionary<string, object> args)
        {
            NetMQSocket socket;
            if(method.accessMode == ZstMethod.RESPONDER)
                socket = m_peers[method.node].request;
            else
                socket = m_publisher;
 
            if (m_peers.Keys.Contains(method.node))
            {
                if (ZstMethod.compareArgLists(m_peers[method.node].methods[method.name].args, args))
                {
                    ZstMethod methodRequest = new ZstMethod(method.name, method.node, method.accessMode, args);
                    ZstIo.send(socket, method.name, methodRequest);

                    if (methodRequest.accessMode == ZstMethod.RESPONDER)
                    {
                        return ZstIo.recv(socket).data;
                    }
                }
            }

            return null;
        }


        /// <summary>Subscribe to a remote method publishing updates</summary>
        public void subscribeToMethod(ZstMethod method, Func<ZstMethod, object> callback)
        {
            if (m_peers.Keys.Contains(method.node))
            {
                m_peers[method.node].methods[method.name].callback = callback;
            }
        }
    }


    /// <summary>Struct to hold message information</summary>
    public struct MethodMessage
    {
        public string method;
        public ZstMethod data;
        public MethodMessage(string methodName, ZstMethod methodData)
        {
            method = methodName;
            data = methodData;
        }
    }
}