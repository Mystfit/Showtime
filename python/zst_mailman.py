import threading
from zst_socket import ZstSocket

class ZstMailman(threading.Thread):

    def __init__(self):
        threading.Thread.__init__(self)
        self.sockets = {}
        self.poller = zmq.Poller()

    def add_socket(self, socket):

        self.sockets[socket] = True
        self.poller.register(socket, zmq.POLLIN)


    def stop(self):
        self.exitFlag = 1

    def run(self):
        while not self.exitFlag:
            socklist = dict(self.poller.poll(3))
            for socket in socklist:
                message = self.socket_recv(zmq.DONTWAIT)
