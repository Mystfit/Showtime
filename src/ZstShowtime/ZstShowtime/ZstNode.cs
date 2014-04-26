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


        // Member variables
        // ----------------
        protected string m_nodeId;
        protected Dictionary<string, ZstMethod> m_methods;
        protected Dictionary<string, ZstMethod> m_internalNodeMethods;
        protected Dictionary<string, ZstPeerLink> m_peers;
        protected string m_stageAddress;
        protected string m_replyAddress;
        protected string m_publisherAddress;

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
            m_publisher = m_ctx.CreatePublisherSocket();
            m_subscriber = m_ctx.CreateSubscriberSocket();

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
                m_stage.Connect(m_stageAddress);
                Console.WriteLine("Stage located at " + m_stage.Options.GetLastEndpoint);
                Console.WriteLine("Node reply on address " + m_replyAddress);
                Console.WriteLine("Node publisher on address " + m_publisherAddress);
            }

            // Subscribe to all incoming messages
            m_subscriber.SubscribeToAll();

            // Bind event listeners to sockets
            m_reply.ReceiveReady += handleReplyRequests;
            m_subscriber.ReceiveReady += receiveMethodUpdate;


            // Intialize Poller
            m_pollerThread = new ZstPoller();
            m_pollerThread.poller.AddSocket(m_subscriber);
            m_pollerThread.poller.AddSocket(m_reply);
            m_pollerThread.Start();
        }

        private void registerInternalMethods()
        {
            m_internalNodeMethods[REPLY_REGISTER_NODE] = new ZstMethod(REPLY_REGISTER_NODE, m_nodeId, ZstMethod.READ, null, replyRegisterNode);
            m_internalNodeMethods[REPLY_REGISTER_METHOD] = new ZstMethod(REPLY_REGISTER_METHOD, m_nodeId, ZstMethod.READ, null, replyRegisterMethod);
            m_internalNodeMethods[REPLY_NODE_PEERLINKS] = new ZstMethod(REPLY_NODE_PEERLINKS, m_nodeId, ZstMethod.READ, null, replyNodePeerlinks);
            m_internalNodeMethods[REPLY_METHOD_LIST] = new ZstMethod(REPLY_METHOD_LIST, m_nodeId, ZstMethod.READ, null, replyRegisterNode);
            m_internalNodeMethods[REPLY_ALL_PEER_METHODS] = new ZstMethod(REPLY_ALL_PEER_METHODS, m_nodeId, ZstMethod.READ, null, replyRegisterNode);
        }

        /// <summary>Close and dispose of all sockets/pollers/threads</summary>
        public bool cleanup()
        {
            m_pollerThread.IsDone = true;
            m_pollerThread.Update();

            m_reply.Dispose();
            m_publisher.Dispose();
            m_subscriber.Dispose();

            if (m_stage != null)
                m_stage.Dispose();

            m_ctx.Dispose();
            return true;
        }

        /// <summary>Incoming request handler</summary>
        protected void handleReplyRequests(object sender, NetMQSocketEventArgs e)
        {
            if (e.ReceiveReady)
            {
                MethodMessage msg = recv(e.Socket);
                if (m_internalNodeMethods.Keys.Contains(msg.method))
                    m_internalNodeMethods[msg.method].run(msg.data);
            }
        }

        /// <summary>Method update handler</summary>
        protected void receiveMethodUpdate(object sender, NetMQSocketEventArgs e)
        {
            if (e.ReceiveReady)
            {
                MethodMessage msg = recv(e.Socket);
                Console.WriteLine("Recieved method '" + msg.method + "' from '" + msg.data.node + "' with value '" + msg.data.output.ToString() + "'");
                if (m_methods.Keys.Contains(msg.method))
                    m_methods[msg.method].run(msg.data);
            }
        }


        // Node registration
        //------------------
        public void requestRegisterNode(){
            requestRegisterNode(m_stage);
        }

        /// <summary>Request this node to be registered on another remote node</summary>
        public void requestRegisterNode(NetMQSocket socket)
        {
            Console.WriteLine("REQ-->: Requesting remote node to register our addresses. Reply:" + m_replyAddress + ", Publisher:" + m_publisherAddress);
            
            Dictionary<string, object> requestArgs = new Dictionary<string, object>(){
                {ZstPeerLink.REPLY_ADDRESS, m_replyAddress},
                {ZstPeerLink.PUBLISHER_ADDRESS, m_publisherAddress}};
            ZstMethod request = new ZstMethod(REPLY_REGISTER_NODE, m_nodeId, "", requestArgs);

            send(socket, REPLY_REGISTER_NODE, request); 
            MethodMessage msg = recv(socket);

            if (msg.method == OK)
                Console.WriteLine("REP<--: Remote node acknowledged our addresses. Reply:" + m_replyAddress + ", Publisher:" + m_publisherAddress);
            else
                Console.WriteLine("REP<--:Remote node returned " + msg.method + " instead of " + OK);
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

            subscribeTo(m_peers[nodeId]);
            send(m_reply, OK);
            Console.WriteLine("Registered node '" + nodeId + "'. Reply:" + m_peers[nodeId].replyAddress + ", Publisher:" + m_peers[nodeId].publisherAddress);
            return null;
        }

        /// <summary> Subscribe to messages coming from an external node</summary>
        public void subscribeTo(ZstPeerLink peer)
        {
            m_subscriber.Connect(peer.publisherAddress);
            m_subscriber.SubscribeToAll();
            m_peers[peer.name] = peer;

            Console.WriteLine("Connected to peer on " + peer.publisherAddress);
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
            send(socket, REPLY_REGISTER_METHOD, m_methods[method]);
            MethodMessage msg = recv(socket);

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

            send(m_reply, OK);
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
            send(socket, REPLY_NODE_PEERLINKS);
            Dictionary<string, object> peerDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(recv(socket).data.output.ToString());
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
            send(m_reply, OK, request);
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
            send(socket, REPLY_METHOD_LIST);
            return ZstMethod.buildLocalMethods((Dictionary<string, object>)recv(socket).data.output);
        }

        /// <summary>Reply with a list of methods this node owns</summary>
        public object replyMethodList(ZstMethod methodData)
        {
            Dictionary<string, object> methodDict = new Dictionary<string, object>();
            foreach (KeyValuePair<string, ZstMethod> method in m_methods)
                methodDict[method.Key] = method.Value.as_dict();
            ZstMethod request = new ZstMethod(REPLY_NODE_PEERLINKS, m_nodeId, "", null);
            request.output = methodDict;
            send(m_reply, OK, request);
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
            send(socket, REPLY_METHOD_LIST);
            return ZstMethod.buildLocalMethods((Dictionary<string, object>)recv(socket).data.output);
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
            send(m_reply, OK, request);
            return null;
        }


        // Remote send/recieve methods
        // ---------------------------
        /// <summary>Send a message to a remote node</summary>
        protected static void send(NetMQSocket socket, string method)
        {
            send(socket, method, null);
        }

        /// <summary>Send a message to a remote node using method info</summary>
        protected static void send(NetMQSocket socket, string method, ZstMethod methodData)
        {
            NetMQMessage message = new NetMQMessage();
            message.Append(method);
            if (methodData != null)
            {
                string data = JsonConvert.SerializeObject(methodData.as_dict());
                JsonConvert.SerializeObject(data, Formatting.Indented);
                message.Append(data);
            }
            else
            {
                message.Append("{}");
            }
            socket.SendMessage(message);
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
                method.output = value;
                send(m_publisher, method.name, method);
            }
        }

        /// <summary>Triggers a remote method with arguments</summary>
        public void updateRemoteMethod(ZstMethod method, Dictionary<string, object> args)
        {
            if (m_peers.Keys.Contains(method.node))
            {
                if (ZstMethod.compareArgLists(args, m_peers[method.node].methods[method.name].args))
                {
                    ZstMethod methodRequest = new ZstMethod(method.name, m_nodeId, method.accessMode, args);
                    send(m_publisher, method.name, methodRequest);
                }
            }
        }

        /// <summary>Receive a message from a remote node</summary>
        protected static MethodMessage recv(NetMQSocket socket)
        {
            return recv(socket, false);
        }

        /// <summary>Receive a message from a remote node</summary>
        protected static MethodMessage recv(NetMQSocket socket, bool dontWait)
        {
            try
            {
                NetMQMessage message = socket.ReceiveMessage(dontWait);
                string method = message[0].ConvertToString();

                //Dictionary<string, object> data = new Dictionary<string, object>();
                string jsonStr = (message[1].ConvertToString());
                Dictionary<string, object> data = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonStr);

                return new MethodMessage(method, ZstMethod.dictToZstMethod(data));
            }
            catch (NetMQException e)
            {

            }
            return new MethodMessage("", null);
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