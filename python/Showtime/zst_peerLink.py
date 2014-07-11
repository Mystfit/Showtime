import zmq
from zst_method import ZstMethod


class ZstPeerLink():

    # Message constants
    REPLY_ADDRESS = "zst_key_rep_address"
    PUBLISHER_ADDRESS = "zst_key_pub_address"
    NAME = "zst_key_name"
    METHOD_LIST = "zst_method_list"

    def __init__(self, name, replyAddress, publisherAddress, methods=None):
        self.name = name
        self.replyAddress = replyAddress
        self.publisherAddress = publisherAddress
        self.request = None
        self.subscriber = None
        self.methods = methods if methods else {}

    def disconnect(self):
        if self.request:
            self.request.stop()
        if self.subscriber:
            self.subscriber.stop()

    def as_dict(self):
        methodlist = {}
        for name, method in self.methods.iteritems():
            methodlist[name] = method.as_dict()

        return {
            ZstPeerLink.NAME: self.name,
            ZstPeerLink.REPLY_ADDRESS: self.replyAddress,
            ZstPeerLink.PUBLISHER_ADDRESS: self.publisherAddress,
            ZstPeerLink.METHOD_LIST: methodlist}

    @staticmethod
    def build_local_peerlinks(peers):
        peerlinks = {}
        for name, peer in peers.iteritems():
            methodList =  ZstMethod.build_local_methods(peer[ZstPeerLink.METHOD_LIST])
            peerlinks[name] = ZstPeerLink(
                peer[ZstPeerLink.NAME],
                peer[ZstPeerLink.REPLY_ADDRESS],
                peer[ZstPeerLink.PUBLISHER_ADDRESS],
                methodList)
        return peerlinks
