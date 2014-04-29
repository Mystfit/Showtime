import threading
import zmq
from zst_socket import ZstSocket

class ZstMailman(threading.Thread):

    TIMEOUT = 2.0

    def __init__(self, ctx, messageCallback):
        threading.Thread.__init__(self)
        self.ctx = ctx
        self.exitFlag = 0
        self.poller = zmq.Poller()
        self.callback = messageCallback
        self.start()

    def stop():
        self.exitFlag = 1
        self.join(ZstMailman.TIMEOUT)

    def run(self):
        while not self.exitFlag:
            self.handle_requests()

    def handle_requests(self):
        socklist = dict(self.poller.poll(ZstMailman.TIMEOUT))
        for socket in socklist:
            self.callback(recv(socket, zmq.DONTWAIT))
