import zmq
import sys
import socket
import json
import time
from zst_io import *
from zst_poller import ZstPoller
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
    REPLY_REGISTER_INCOMING = "reply_register_incoming"
    REPLY_NODE_PEERLINKS = "reply_node_peerlinks"
    REPLY_METHOD_LIST = "reply_list_methods"
    REPLY_ALL_PEER_METHODS = "reply_all_peer_methods"
    DISCONNECT_PEER = "disconnect_peer"

    def __init__(self, nodeId, stageAddress=None):
        self.id = nodeId
        self.methods = {}
        self.peers = {}
        self.stageAddress = stageAddress
        self.replyAddress = None
        self.publisherAddress = None

        self.ctx = zmq.Context()
        self.reply = self.ctx.socket(zmq.REP)
        self.publisher = self.ctx.socket(zmq.PUB)
        self.subscriber = self.ctx.socket(zmq.SUB)
        self.stage = self.ctx.socket(zmq.REQ) if stageAddress else None
        self.pollerThread = ZstPoller(self.ctx, self.receive_method_update)

        # Binding ports
        address = 'tcp://{0}:*'.format(socket.gethostbyname(socket.gethostname()))
        self.reply.bind(address)
        self.replyAddress = self.reply.getsockopt(zmq.LAST_ENDPOINT)

        self.publisher.bind(address)
        self.publisherAddress = self.publisher.getsockopt(zmq.LAST_ENDPOINT)

        # Subscribe to all incoming messages
        self.subscriber.setsockopt(zmq.SUBSCRIBE, "")

        # Connect to stage
        if self.stageAddress:
            self.stage.connect(self.stageAddress)

        # Initialize poller
        self.pollerThread.poller.register(self.reply, zmq.POLLIN)
        self.pollerThread.poller.register(self.subscriber, zmq.POLLIN)
        self.pollerThread.start()


    def close(self):
        print "PUB-->: Announcing that we're leaving."
        send(self.publisher, ZstNode.DISCONNECT_PEER, ZstMethod(ZstNode.DISCONNECT_PEER, self.id))

        if self.stage:
            self.stage.close()

        print "Exiting poller"
        self.pollerThread.exitFlag = 1
        self.pollerThread.join(2.0)
        if self.pollerThread.isAlive():
            print "Thread alive. Exiting anyway"

        self.reply.close()
        self.publisher.close()
        self.subscriber.close()

        self.ctx.destroy()
        print "Cleanup complete"

    def disconnect_peer(self, methodData):
        print "Peer '{0}' is leaving.".format(methodData.node)
        if methodData.node in self.peers:
            print "Disconnecting address {0}".format(self.peers[methodData.node].publisherAddress)
            self.subscriber.disconnect(self.peers[methodData.node].publisherAddress)
            self.peers[methodData.node].disconnect()
            del self.peers[methodData.node]
        print "Peers now contains:"
        for peer in self.peers.iteritems():
            print peer

    def listen(self):
        print 'Node listening for requests...'
        try:
            while True:
                time.sleep(4)
        except KeyboardInterrupt:
            print "\nFinished"
            self.close()

    # ----------------------------------------------
    # Recieve updates from nodes we're interested in
    # ----------------------------------------------
    def receive_method_update(self, message):
        if message.method in self.methods:
            print "Matched local method '{0}' from '{1}' with value '{2} and args {3}'".format(
                message.method,
                message.data.node,
                message.data.output,
                message.data.args)
            self.methods[message.method].run(message.data)

        if hasattr(self, message.method):
            print "Matched internal method {0}".format(message.method)
            getattr(self, message.method)(message.data)

    # ------------------------------------------------------
    # Request/Reply this node be registered to a remote peer
    # ------------------------------------------------------
    def request_register_node(self, nodesocket=None):
        socket = nodesocket if nodesocket else self.stage

        # Request a port for this equipment to bind as its outgoing sender
        print "REQ-->: Requesting remote node to register our addresses. Reply:{0}, Publisher:{1}".format(self.replyAddress, self.publisherAddress)
        request = ZstMethod(
            name=ZstNode.REPLY_REGISTER_NODE,
            node=self.id,
            args={
                ZstPeerLink.REPLY_ADDRESS: self.replyAddress,
                ZstPeerLink.PUBLISHER_ADDRESS: self.publisherAddress
            })
        send(socket, ZstNode.REPLY_REGISTER_NODE, request)
        message = recv(socket)

        if message.method == ZstNode.OK:
            print "REP<--: Remote node acknowledged our addresses. Reply:{0}, Publisher:{1}".format(self.replyAddress, self.publisherAddress)
            return True
        else:
            print "REP<--:Remote node returned {0} instead of {1}.".format(message.method, ZstNode.OK)
        return False

    def reply_register_node(self, methodData):
        if methodData.node in self.peers:
            print "'{0}' node already registered. Overwriting".format(methodData.node)

        self.peers[methodData.node] = ZstPeerLink(
            methodData.node,
            methodData.args[ZstPeerLink.REPLY_ADDRESS],
            methodData.args[ZstPeerLink.PUBLISHER_ADDRESS])

        self.subscribe_to(self.peers[methodData.node])

        send(self.reply, ZstNode.OK)
        print "Registered node '{0}'. Reply:{1}, Publisher:{2}".format(methodData.node, self.peers[methodData.node].replyAddress, self.peers[methodData.node].publisherAddress)

    # -------------------------------------------
    # Subscribe to messages from an external node
    # -------------------------------------------
    def subscribe_to(self, peerlink):
        # Connect to peer
        self.subscriber.connect(peerlink.publisherAddress)
        self.subscriber.setsockopt(zmq.SUBSCRIBE, '')
        self.peers[peerlink.name] = peerlink

        # Register with the event poller
        print "Connected to peer on", self.subscriber.getsockopt(zmq.LAST_ENDPOINT)

    def connect_to_peer(self, peer):
        socket = self.ctx.socket(zmq.REQ)
        socket.connect(peer.replyAddress)
        if self.request_register_node(socket):
            if not peer.name in self.peers:
                self.peers[peer.name] = peer
            self.peers[peer.name].request = socket

    # ---------------------------------------
    # Request/reply a method to be registered
    # ---------------------------------------
    def request_register_method(self, method, mode, args=None, callback=None, nodesocket=None):
        socket = nodesocket if nodesocket else self.stage
        args = args if args else {}
        print "REQ-->: Registering method '%s' with remote node." % method

        # Register local copy of our method first
        self.methods[method] = ZstMethod(method, self.id, mode, args, callback)

        # Make remote copy of local method on stage
        send(socket, ZstNode.REPLY_REGISTER_METHOD, self.methods[method])
        message = recv(socket)

        if message.method == ZstNode.OK:
            print "REP<--: Remote node acknowledged our method '{0}'".format(method)
        else:
            print "REP<--: Remote node returned {0} instead of {1}.".format(message.method, ZstNode.OK)

    def reply_register_method(self, methodData):
        if methodData.name in self.methods:
            print "'{0}' method already registered. Overwriting".format(methodData.name)

        self.peers[methodData.node].methods[methodData.name] = methodData
        send(self.reply, ZstNode.OK)
        print "Registered method '{0}'. Origin:{1}, AccessMode:{2}, Args:{3}".format(methodData.name, methodData.node, methodData.accessMode, methodData.args)

    # -----------------------------------------------------------------
    # Request/Reply a list of all available nodes connected to this one
    # -----------------------------------------------------------------
    def request_node_peerlinks(self, nodesocket=None):
        socket = nodesocket if nodesocket else self.stage
        send(socket, ZstNode.REPLY_NODE_PEERLINKS)
        return ZstPeerLink.build_local_peerlinks(recv(socket).data.output)

    def reply_node_peerlinks(self, args):
        peerList = {}
        for peerName, peerData in self.peers.iteritems():
            peerList[peerName] = peerData.as_dict()

        request = ZstMethod(
            name=ZstNode.REPLY_NODE_PEERLINKS,
            node=self.id,
            output=peerList)
        send(self.reply, ZstNode.OK, request)

    # -------------------------------------------------------------------
    # Request/Reply a list of all available methods provided by this node
    # -------------------------------------------------------------------
    def request_method_list(self, nodesocket=None):
        socket = nodesocket if nodesocket else self.stage
        send(socket, ZstNode.REPLY_METHOD_LIST)
        return ZstMethod.build_local_methods(recv(socket).data)

    def reply_method_list(self, args):
        methodList = {}
        for name, method in self.methods.iteritems():
            methodList[name] = method.as_dict()

        request = ZstMethod(
            name=ZstNode.REPLY_METHOD_LIST,
            node=self.nodeId,
            output=methodList)
        send(self.reply, ZstNode.OK, request)

    # -------------------------------------------------------------
    # Request/Reply a list of all available methods provided by all
    # connected peers to the remote node
    # -------------------------------------------------------------
    def request_all_peer_methods(self, nodesocket=None):
        socket = nodesocket if nodesocket else self.stage
        send(socket, ZstNode.REPLY_ALL_PEER_METHODS)
        return ZstMethod.build_local_methods(recv(socket).data)

    def reply_all_peer_methods(self, args):
        methodList = {}
        for peerName, peer in self.peers.iteritems():
            for methodName, method in peer.methods.iteritems():
                methodList[methodName] = method.as_dict()
        request = ZstMethod(
            name=REPLY_ALL_PEER_METHODS,
            node=self.nodeId,
            output=methodList)
        send(self.reply, ZstNode.OK, request)

    # ----------------------------------------------------------
    # Publish updates from a node method to all interested nodes
    # ----------------------------------------------------------
    def update_local_method_by_name(self, methodname, methodvalue):
        method = self.methods[methodname]
        return self.update_local_method(method, methodvalue)

    def update_local_method(self, method, methodvalue):
        # Only update local values on methods WE own
        if method.node == self.id:
            method.output = methodvalue
            send(self.publisher, method.name, method)
        else:
            print "We don't own this method!"
        return methodvalue

    def update_remote_method_by_name(self, methodname, methodargs=None, echosocket=False):
        method = self.methods[methodname]
        return self.update_remote_method(method, methodargs)

    def update_remote_method(self, method, methodargs=None, echosocket=False):
        socket = echosocket if echosocket else self.publisher
        if method.node in self.peers:
            methodDict = method.as_dict()
            if ZstMethod.compare_arg_lists(methodDict[ZstMethod.METHOD_ARGS], methodargs):
                methodData = ZstMethod(
                    name=method.name,
                    node=method.node,
                    args=methodargs)
                send(socket, method.name, methodData)
                if echosocket:
                    return recv(socket)
            else:
                print "Mismatch on arguments being sent to remote node!"
                raise
        else:
            print "Method destination node not in connected peers list!"
        pass
       
    def echo_remote_method(self, method, methodargs=None):
        return self.update_remote_method(method, methodargs, self.peers[method.node].request)


# Test cases
if __name__ == '__main__':

    if len(sys.argv) > 2:

        node = ZstNode(sys.argv[1], sys.argv[2])
        node.request_register_node(node.stage)
        node.request_register_method(
            "testMethod", ZstMethod.WRITE, {'arg1': 0, 'arg2': 0, 'arg3': 0})
        node.request_register_method(
            "anotherTestMethod", ZstMethod.READ, {'arg1': 1, 'arg2': 2, 'arg3': 3})

        print "\nListing stage nodes:"
        print "--------------------"
        nodeList = node.request_node_peerlinks(node.stage)
        for name, peer in nodeList.iteritems():
            print name, json.dumps(peer.as_dict(), indent=1, sort_keys=True)

        node.listen()

    print "Please provide a name for this node!"
    sys.exit(0)
