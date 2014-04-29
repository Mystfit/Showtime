import zmq
from zst_node import ZstNode


class ZstStage(ZstNode):

    def __init__(self):
        ZstNode.__init__(self, 'stage')
        self.start()

    def start(self):
        port = 6000
        address = "tcp://*:" + str(port)
        self.reply.socket.bind(address)
        print "Stage active on address " + self.reply.socket.getsockopt(zmq.LAST_ENDPOINT)

if __name__ == '__main__':
    stage = ZstStage()
    stage.listen()
