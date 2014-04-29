import threading
import zmq
import json
import Queue
from zst_method import ZstMethod


class ZstSocket(threading.Thread):
    def __init__(self, ctx, sockettype, name=None):
        threading.Thread.__init__(self)
        self.ctx = ctx
        self.exitFlag = 0
        self.poller = None
        self.name = name if name else "socket"

        self.socket = self.ctx.socket(sockettype)
        self.socket.setsockopt(zmq.LINGER, 0)
        self.inMailbox = Queue.LifoQueue()
        self.outMailbox = Queue.LifoQueue()

        if sockettype == zmq.REP or sockettype == zmq.SUB:
            self.poller = zmq.Poller()
            self.poller.register(self.socket, zmq.POLLIN)

        self.start()

    def stop(self):
        self.exitFlag = 1
        self.join(2.0)

    def run(self):
        while not self.exitFlag:
            if self.poller:
                self.handle_requests()
            else:
                self.handle_outgoing()
        print self.name + " received kill signal"
        self.socket.close()

    def handle_outgoing(self):
        print "Checking mailbox..."
        try:
            message = self.outMailbox.get(True, 3)
        except Queue.Empty:
            return
        print "Message in mailbox. Sending..."
        self.socket_send(message.method, message.data)
        print "Sent"

    def handle_requests(self):
        if self.poller:
            socklist = dict(self.poller.poll(3))
            for socket in socklist:
                message = self.socket_recv(zmq.DONTWAIT)
                if message:
                    print "Received message {0}. Putting in mailbox.".format(message.method)
                    self.inMailbox.put(message)

    def send(self, method, methodData=None):
        print "Queuing a send in mailbox..."
        self.outMailbox.put(MethodMessage(method, methodData))

    def socket_send(self, method, methodData=None):
        try:
            outData = {}
            if methodData:
                outData = methodData.as_dict()
            self.socket.send_multipart([str(method), json.dumps(outData)])
        except Exception, e:
            print e

    def recv(self):
        try:
            print self.inMailbox
            return self.inMailbox.get(True, 3)
        except Queue.Empty:
            return None

    def socket_recv(self, noblock=None):
        try:
            msg = self.socket.recv_multipart(zmq.NOBLOCK) if noblock else self.socket.recv_multipart()
            method = msg[0]
            method = method if method else None
            data = json.loads(msg[1]) if msg[1] else None
            if data:
                methodData = ZstMethod(
                    name=data[ZstMethod.METHOD_NAME],
                    node=data[ZstMethod.METHOD_ORIGIN_NODE],
                    accessMode=data[ZstMethod.METHOD_ACCESSMODE],
                    args=data[ZstMethod.METHOD_ARGS],
                    output=data[ZstMethod.METHOD_OUTPUT])
                return MethodMessage(method=method, data=methodData)
            else:
                return MethodMessage(method=method, data=data)
        except zmq.ZMQError, e:
            print e
        return None

class MethodMessage():
    def __init__(self, method, data):
        self.method = method
        self.data = data
