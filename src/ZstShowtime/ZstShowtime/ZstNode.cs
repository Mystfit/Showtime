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
            Console.WriteLine("Node reply on address " + m_replyAddress);

            port = m_publisher.BindRandomPort(address);
            m_publisherAddress = address + ":" + port;
            Console.WriteLine("Node publisher on address " + m_publisherAddress);

            // Connect to stage
            if (!string.IsNullOrEmpty(stageAddress))
                m_stage = m_ctx.CreateRequestSocket();
            m_stage.Connect(m_stageAddress);

            // Subscribe to all incoming messages
            m_subscriber.SubscribeToAll();

            // Bind event listeners to sockets
            m_reply.ReceiveReady += handleReplyRequests;
            m_subscriber.ReceiveReady += receiveMethodUpdate;

            Console.WriteLine("Stage located at " + m_stage.Options.GetLastEndpoint);

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
            m_internalNodeMethods[REPLY_NODE_PEERLINKS] = new ZstMethod(REPLY_NODE_PEERLINKS, m_nodeId, ZstMethod.READ, null, replyRegisterNode);
            m_internalNodeMethods[REPLY_METHOD_LIST] = new ZstMethod(REPLY_METHOD_LIST, m_nodeId, ZstMethod.READ, null, replyRegisterNode);
            m_internalNodeMethods[REPLY_ALL_PEER_METHODS] = new ZstMethod(REPLY_ALL_PEER_METHODS, m_nodeId, ZstMethod.READ, null, replyRegisterNode);
        }

        /// <summary>
        /// Close and dispose of all sockets/pollers/threads
        /// </summary>
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

        /// <summary>
        /// Incomin request handler.
        /// </summary>
        protected void handleReplyRequests(object sender, NetMQSocketEventArgs e)
        {
            if (e.ReceiveReady)
            {
                MethodMessage msg = recv(e.Socket);
                if (m_internalNodeMethods.Keys.Contains(msg.method))
                    m_internalNodeMethods[msg.method].run(ZstMethod.dictToZstMethod(msg.data));
            }
        }

        /// <summary>
        /// Method update handler.
        /// </summary>
        protected void receiveMethodUpdate(object sender, NetMQSocketEventArgs e)
        {
            if (e.ReceiveReady)
            {
                MethodMessage msg = recv(e.Socket);
                if (m_methods.Keys.Contains(msg.method))
                    m_methods[msg.method].run(ZstMethod.dictToZstMethod(msg.data));
            }
        }

        public void requestRegisterNode()
        {
            requestRegisterNode(m_stage);
        }

        /// <summary>
        /// Request this node to be registered on another remote node.
        /// </summary>
        public void requestRegisterNode(NetMQSocket socket)
        {
            Console.WriteLine("REQ-->: Requesting remote node to register our addresses. Reply:" + m_replyAddress + ", Publisher:" + m_publisherAddress);
            send(socket, REPLY_REGISTER_NODE, new Dictionary<string, object>(){
                {ZstPeerLink.NAME, m_nodeId},
                {ZstPeerLink.REPLY_ADDRESS, m_replyAddress},
                {ZstPeerLink.PUBLISHER_ADDRESS, m_publisherAddress}});
            MethodMessage msg = recv(socket);

            if (msg.method == OK)
                Console.WriteLine("REP<--: Remote node acknowledged our addresses. Reply:" + m_replyAddress + ", Publisher:" + m_publisherAddress);
            else
                Console.WriteLine("REP<--:Remote node returned " + msg.method + " instead of " + OK);
        }

        /// <summary>
        /// Reply to another node's request for registration.
        /// </summary>
        protected object replyRegisterNode(ZstMethod methodData)
        {
            if (methodData.args.Keys.Contains(ZstPeerLink.NAME))
                if (m_peers.Keys.Contains((string)methodData.args[ZstPeerLink.NAME]))
                    Console.WriteLine("'" + m_nodeId + "' already registered. Overwriting");

            string nodeId = (string)methodData.args[ZstPeerLink.NAME];
            m_peers[nodeId] = new ZstPeerLink(
                nodeId,
                (string)methodData.args[ZstPeerLink.REPLY_ADDRESS],
                (string)methodData.args[ZstPeerLink.PUBLISHER_ADDRESS]);

            subscribeTo(m_peers[nodeId]);
            send(m_reply, OK);
            Console.WriteLine("Registered node '" + nodeId + "'. Reply:" + m_peers[nodeId].replyAddress + ", Publisher:" + m_peers[nodeId].publisherAddress);
            return null;
        }

        /// <summary>
        /// Subscribe to messages coming from an external node.
        /// </summary>
        public void subscribeTo(ZstPeerLink peer)
        {
            m_subscriber.Connect(peer.publisherAddress);
            m_subscriber.SubscribeToAll();
            m_peers[peer.name] = peer;

            Console.WriteLine("Connected to peer on " + peer.publisherAddress);
        }

        public void requestRegisterMethod(string method, string accessMode)
        {
            requestRegisterMethod(method, accessMode, null, null, m_stage);
        }
        public void requestRegisterMethod(string method, string accessMode, Dictionary<string, object> args)
        {
            requestRegisterMethod(method, accessMode, args, null, m_stage);
        }
        public void requestRegisterMethod(string method, string accessMode, string[] args)
        {
            Dictionary<string, object> argsDict = new Dictionary<string, object>();
            foreach (string arg in args)
                argsDict[arg] = "";
            requestRegisterMethod(method, accessMode, argsDict, null, m_stage);
        }
        public void requestRegisterMethod(string method, string accessMode, Dictionary<string, object> args, NetMQSocket socket)
        {
            requestRegisterMethod(method, accessMode, args, null, socket);
        }
        public void requestRegisterMethod(string method, string accessMode, Dictionary<string, object> args, Func<ZstMethod, object> callback)
        {
            requestRegisterMethod(method, accessMode, args, callback, m_stage);
        }

        /// <summary>
        /// Registers a local method on a remote node
        /// </summary>
        public void requestRegisterMethod(string method, string accessMode, Dictionary<string, object> args, Func<ZstMethod, object> callback, NetMQSocket socket)
        {
            Console.WriteLine("REQ-->: Registering method " + method + " with remote node.");

            //Register local copy of our method first
            m_methods[method] = new ZstMethod(method, m_nodeId, accessMode, args, callback);

            //Register method copy on remote node
            send(socket, REPLY_REGISTER_METHOD, m_methods[method].as_dict());
            MethodMessage msg = recv(socket);

            if (msg.method == OK)
                Console.WriteLine("REP<--: Remote node acknowledged our method '" + method + "'");
            else
                Console.WriteLine("REP<--:Remote node returned " + msg.method + " instead of " + OK);
        }

        /// <summary>
        /// Reply to another node's method registration request.
        /// </summary>
        protected object replyRegisterMethod(ZstMethod methodData)
        {
            if (methodData.args.Keys.Contains(ZstMethod.METHOD_NAME))
                if (m_methods.Keys.Contains((string)methodData.args[ZstMethod.METHOD_NAME]))
                    Console.WriteLine("'" + (string)methodData.args[ZstMethod.METHOD_NAME] + "' already registered. Overwriting");

            string methodName = (string)methodData.args[ZstMethod.METHOD_NAME];
            string origin = (string)methodData.args[ZstMethod.METHOD_ORIGIN_NODE];

            ZstMethod localMethod = new ZstMethod(
                methodName,
                origin,
                (string)methodData.args[ZstMethod.METHOD_ACCESSMODE],
                (Dictionary<string, object>)methodData.args[ZstMethod.METHOD_ARGS]
            );

            m_peers[origin].methods[methodName] = localMethod;

            send(m_reply, OK);
            Console.WriteLine("Registered method '" + localMethod.name + "'. Origin:" + localMethod.node + ", AccessMode:" + localMethod.accessMode + ", Args:" + localMethod.args);
            return null;
        }

        public Dictionary<string, ZstPeerLink> requestNodePeerlinks()
        {
            return requestNodePeerlinks(m_stage);
        }

        /// <summary>
        /// Request a dictionary of peers nodes linked to the remote node
        /// </summary>
        public Dictionary<string, ZstPeerLink> requestNodePeerlinks(NetMQSocket socket)
        {
            send(socket, REPLY_NODE_PEERLINKS);
            return ZstPeerLink.buildLocalPeerlinks((Dictionary<string, object>)recv(socket).data);
        }

        /// <summary>
        /// Reply to another node's request for this node's linked peers
        /// </summary>
        protected object replyNodePeerlinks(ZstMethod methodData)
        {
            Dictionary<string, object> peerDict = new Dictionary<string, object>();
            foreach (KeyValuePair<string, ZstPeerLink> peer in m_peers)
                peerDict[peer.Key] = peer.Value.as_dict();
            send(m_reply, OK, peerDict);
            return null;
        }

        public Dictionary<string, ZstMethod> requestMethodList()
        {
            return requestMethodList(m_stage);
        }

        /// <summary>
        /// Request a dictionary of methods on a remote node
        /// </summary>
        public Dictionary<string, ZstMethod> requestMethodList(NetMQSocket socket)
        {
            send(socket, REPLY_METHOD_LIST);
            return ZstMethod.buildLocalMethods((Dictionary<string, object>)recv(socket).data);
        }

        /// <summary>
        /// Reply with a list of methods this node owns
        /// </summary>
        public object replyMethodList(ZstMethod methodData)
        {
            Dictionary<string, object> methodDict = new Dictionary<string, object>();
            foreach (KeyValuePair<string, ZstMethod> method in m_methods)
                methodDict[method.Key] = method.Value.as_dict();
            send(m_reply, OK, methodDict);
            return null;
        }


        public Dictionary<string, ZstMethod> requestAllPeerMethods()
        {
            return requestAllPeerMethods(m_stage);
        }

        /// <summary>
        /// Request a list of all available methods provided by all connected peers on the remote node
        /// </summary>
        public Dictionary<string, ZstMethod> requestAllPeerMethods(NetMQSocket socket)
        {
            send(socket, REPLY_METHOD_LIST);
            return ZstMethod.buildLocalMethods((Dictionary<string, object>)recv(socket).data);
        }

        /// <summary>
        /// Reply with a list of methods this node owns
        /// </summary>
        public object replyAllPeerMethods(ZstMethod methodData)
        {
            Dictionary<string, object> methodDict = new Dictionary<string, object>();
            foreach (KeyValuePair<string, ZstPeerLink> peer in m_peers)
            {
                foreach (KeyValuePair<string, ZstMethod> method in peer.Value.methods)
                    methodDict[method.Key] = method.Value.as_dict();
            }
            send(m_reply, OK, methodDict);
            return null;
        }

        /// <summary>
        /// Send a message to a remote node.
        /// </summary>
        protected static void send(NetMQSocket socket, string method)
        {
            send(socket, method, null);
        }

        /// <summary>
        /// Send a message to a remote node using method info.
        /// </summary>
        protected static void send(NetMQSocket socket, string method, Dictionary<string, object> methodData)
        {
            NetMQMessage message = new NetMQMessage();
            message.Append(method);
            if (methodData != null)
                message.Append(JsonConvert.SerializeObject(methodData));
            else
                message.Append("{}");
            socket.SendMessage(message);
        }

        protected static MethodMessage recv(NetMQSocket socket)
        {
            return recv(socket, false);
        }

        /// <summary>
        /// Receive a message from a remote node.
        /// </summary>
        protected static MethodMessage recv(NetMQSocket socket, bool dontWait)
        {
            try
            {
                NetMQMessage message = socket.ReceiveMessage(dontWait);
                string method = message[0].ConvertToString();

                //Dictionary<string, object> data = new Dictionary<string, object>();
                string jsonStr = (message[1].ConvertToString());
                Dictionary<string, object> data = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonStr);

                return new MethodMessage(method, data);
            }
            catch (NetMQException e)
            {

            }
            return new MethodMessage("", null);
        }
    }


    /// <summary>
    /// Struct to hold message information.
    /// </summary>
    public struct MethodMessage
    {
        public string method;
        public Dictionary<string, object> data;
        public MethodMessage(string methodName, Dictionary<string, object> methodData)
        {
            method = methodName;
            data = methodData;
        }
    }
}