import threading
import zmq
from zst_io import *


class ZstPoller(threading.Thread):

    def __init__(self, ctx, messageCallback):
        threading.Thread.__init__(self)
        self.ctx = ctx
        self.exitFlag = 0
        self.poller = zmq.Poller()
        self.callback = messageCallback

    def stop():
        self.exitFlag = 1

    def run(self):
        while not self.exitFlag:
            #print self.exitFlag
            self.handle_requests()

    def handle_requests(self):
        socklist = dict(self.poller.poll(3))
        for socket in socklist:
            self.callback(recv(socket, zmq.DONTWAIT))
