import zmq
import sys
import socket
import json
from zst_method import ZstMethod
from zst_peerLink import ZstPeerLink


class ZstNode(object):

    # Message constants
    REPLY = "zst_reply"
    OK = "zst_ok"

    # Event action types
    PUBLISH_METHODS_CHANGED = "zst_act_announce_method_change"
    REPLY_REGISTER_METHOD = "reply_register_method"
    REPLY_REGISTER_NODE = "reply_register_node"
    REPLY_NODE_PEERLINKS = "reply_node_peerlinks"
    REPLY_METHOD_LIST = "reply_list_methods"
    REPLY_ALL_PEER_METHODS = "reply_all_peer_methods"

    def __init__(self, nodeId, stageAddress=None):

        self.id = nodeId
        self.methods = {}
        self.peers = {}

        # Sockets
        ctx = zmq.Context()
        self.reply = ctx.socket(zmq.REP)
        self.publisher = ctx.socket(zmq.PUB)
        self.subscriber = ctx.socket(zmq.SUB)
        self.stage = ctx.socket(zmq.REQ) if stageAddress else None

        # Binding ports
        address = 'tcp://{0}:*'.format(socket.gethostbyname(socket.gethostname()))
        self.reply.bind(address)
        self.replyAddress = self.reply.getsockopt(zmq.LAST_ENDPOINT)

        self.publisher.bind(address)
        self.publisherAddress = self.publisher.getsockopt(zmq.LAST_ENDPOINT)

        # Connect to stage
        if stageAddress:
            self.stage.connect(stageAddress)

        # Initialize poller
        self.poller = zmq.Poller()
        self.poller.register(self.reply, zmq.POLLIN)
        self.poller.register(self.publisher, zmq.POLLIN)

    def listen(self):
        while True:
            try:
                self.handle_requests(dict(self.poller.poll()))
            except KeyboardInterrupt:
                print "\nFinished"
                break

    # ----------
    # Event loop
    # ----------
    def handle_requests(self, socklist):
        if self.subscriber in socklist:
            message = ZstNode.recv(self.subSocket)
            print message
        if self.reply in socklist:
            self.handle_replies(ZstNode.recv(self.reply))
    
    # -------------
    # Reply handler
    # -------------
    def handle_replies(self, message):
        getattr(self, message.method)(message.data)

    # ------------------------------------------------------
    # Request/Reply this node be registered to a remote peer
    # ------------------------------------------------------
    def request_register_node(self, nodesocket=None):
        socket = nodesocket if nodesocket else self.stage
        
        # Request a port for this equipment to bind as its outgoing sender
        print "REQ-->: Requesting remote node to register our addresses. Reply:{0}, Publisher:{1}".format(self.replyAddress, self.publisherAddress)

        ZstNode.send(socket, ZstNode.REPLY_REGISTER_NODE, {
            ZstPeerLink.OWNER: self.id, 
            ZstPeerLink.REPLY_ADDRESS: self.replyAddress, 
            ZstPeerLink.PUBLISHER_ADDRESS: self.publisherAddress})
        message = ZstNode.recv(socket)

        if message.method == ZstNode.OK:
            print "REP<--: Remote node acknowledged our addresses. Reply:{0}, Publisher:{1}".format(self.replyAddress, self.publisherAddress)
        else:
            print "REP<--:Remote node returned {0} instead of {1}.".format(message.action, ZstNode.OK)

    def reply_register_node(self, args):
        if hasattr(args, ZstPeerLink.OWNER):
            if args[ZstPeerLink.OWNER] in self.peers:
                print "'{0}' node already registered. Overwriting".format(args[ZstPeerLink.OWNER])

        nodeid = args[ZstPeerLink.OWNER]
        self.peers[nodeid] = ZstPeerLink(
            args[ZstPeerLink.REPLY_ADDRESS], 
            args[ZstPeerLink.PUBLISHER_ADDRESS])
        
        ZstNode.send(self.reply, ZstNode.OK)
        print "Registered node '{0}'. Reply:{1}, Publisher:{2}".format(nodeid, self.peers[nodeid].replyAddress, self.peers[nodeid].publisherAddress)

    # ---------------------------------------
    # Request/reply a method to be registered
    # ---------------------------------------
    def request_register_method(self, method, args, mode, nodesocket=None):
        socket = nodesocket if nodesocket else self.stage
        print "REQ-->: Registering method '%s' with remote node." % method

        # Register local copy of our method first
        self.methods[method] = ZstMethod(method, self.id, mode, args)

        # Make remote copy of local method on stage
        ZstNode.send(socket, ZstNode.REPLY_REGISTER_METHOD, self.methods[method].as_dict())
        message = ZstNode.recv(socket)

        if message.method == ZstNode.OK:
            print "REP<--: Remote node acknowledged our method '{0}'".format(method)
        else:
            print "REP<--: Remote node returned {0} instead of {1}.".format(message.action, ZstNode.OK)

    def reply_register_method(self, args):
        if hasattr(args, ZstMethod.METHOD_NAME):
            if args[ZstMethod.METHOD_NAME] in self.methods:
                print "'{0}' method already registered. Overwriting".format(args[ZstMethod.METHOD_NAME])
        methodname = args[ZstMethod.METHOD_NAME]

        localMethod = ZstMethod(
            methodname,
            args[ZstMethod.METHOD_ORIGIN_NODE],
            args[ZstMethod.METHOD_ACCESSMODE],
            args[ZstMethod.METHOD_ARGS])

        self.peers[args[ZstMethod.METHOD_ORIGIN_NODE]].methods[methodname] = localMethod

        ZstNode.send(self.reply, ZstNode.OK)
        print "Registered method '{0}'. Origin:{1}, AccessMode:{2}, Args:{3}".format(localMethod.name, localMethod.node, localMethod.accessMode, localMethod.args)

    # -----------------------------------------------------------------
    # Request/Reply a list of all available nodes connected to this one
    # -----------------------------------------------------------------
    def request_node_peerlinks(self, nodesocket=None):
        socket = nodesocket if nodesocket else self.stage
        ZstNode.send(socket, ZstNode.REPLY_NODE_PEERLINKS)
        return ZstPeerLink.build_local_peerlinks(ZstNode.recv(socket).data)

    def reply_node_peerlinks(self, args):
        peerList = {}
        for peerName, peerData in self.peers.iteritems():
            peerList[peerName] = peerData.as_dict()
        ZstNode.send(self.reply, ZstNode.OK, peerList)

    # -------------------------------------------------------------------
    # Request/Reply a list of all available methods provided by this node
    # -------------------------------------------------------------------
    def request_method_list(self, nodesocket=None):
        socket = nodesocket if nodesocket else self.stage
        ZstNode.send(socket, ZstNode.REPLY_METHOD_LIST)
        return ZstMethod.build_local_methods(ZstNode.recv(socket).data)

    def reply_method_list(self, args):
        methodList = {}
        for name, method in self.methods.iteritems():
            methodList[name] = method.as_dict()
        ZstNode.send(self.reply, ZstNode.OK, methodList)

    # -------------------------------------------------------------
    # Request/Reply a list of all available methods provided by all 
    # connected peers to the remote node
    # -------------------------------------------------------------
    def request_all_peer_methods(self, nodesocket=None):
        socket = nodesocket if nodesocket else self.stage
        ZstNode.send(socket, ZstNode.REPLY_ALL_PEER_METHODS)
        return ZstMethod.build_local_methods(ZstNode.recv(socket).data)

    def reply_all_peer_methods(self, args):
        methodList = {}
        for peerName, peer in self.peers.iteritems():
            for methodName, method in peer.methods.iteritems():
                methodList[methodName] = method.as_dict()
        ZstNode.send(self.reply, ZstNode.OK, methodList)

    # -----------------------
    # Send / Recieve handlers
    # -----------------------
    @staticmethod
    def send(socket, method, data=None):
        socket.send_multipart([method, json.dumps(data)])

    @staticmethod
    def recv(socket):
        msg = socket.recv_multipart()
        method, data = msg
        method = method if method else None
        data = json.loads(data) if data else None
        return MethodMessage(method=method, data=data)


class MethodMessage():
    def __init__(self, method, data):
        self.method = method
        self.data = data


# Test cases
if __name__ == '__main__':

    if len(sys.argv) > 2:
        
        node = ZstNode(sys.argv[1], sys.argv[2])
        node.request_register_node(node.stage)
        #node.request_register_method("testMethod", ['arg1', 'arg2', 'arg3'], ZstMethod.WRITE)
        node.request_register_method("anotherTestMethod", ['arg1', 'arg2', 'arg3'], ZstMethod.READ)
        node.request_register_method("woobly", ['arg1', 'arg2', 'arg3'], ZstMethod.READ)

        print "\nListing stage nodes:"
        print "--------------------"
        nodeList = node.request_node_peerlinks(node.stage)
        for name, peer in nodeList.iteritems():
            print name, json.dumps(peer.as_dict(), indent=1, sort_keys=True)   

        node.listen()
    
    print "Please provide a name for this node!"
    sys.exit(0)

