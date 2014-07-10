import threading
import Queue


# Delivers messages to the main thread when available
class ZstMailman(threading.Thread):

    TIMEOUT = 2.0

    def __init__(self, messageCallback):
        threading.Thread.__init__(self)
        self.exitFlag = 0
        self.callback = messageCallback
        self.incoming = Queue.LifoQueue()

    def stop(self):
        self.exitFlag = 1
        self.join(ZstMailman.TIMEOUT)

    def put(self, message):
        self.incoming.put(message)

    def run(self):
        while not self.exitFlag:
            try:
                message = self.incoming.get(True, ZstMailman.TIMEOUT)
                if message:
                    self.callback(message)
            except Queue.Empty:
                pass
