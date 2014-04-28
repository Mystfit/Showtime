import zmq
import json
from zst_method import ZstMethod


class MethodMessage():

    def __init__(self, method, data):
        self.method = method
        self.data = data


# -----------------------
# Send / Recieve handlers
# -----------------------
def send(socket, method, methodData=None):
    try:
        outData = {}
        if methodData:
            outData = methodData.as_dict()
        socket.send_multipart([str(method), json.dumps(outData)])
    except Exception, e:
        print e


def recv(socket, noblock=None):
    try:
        msg = socket.recv_multipart(
            zmq.NOBLOCK) if noblock else socket.recv_multipart()
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
