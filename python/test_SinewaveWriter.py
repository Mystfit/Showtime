import sys
import math
import time
import threading
from zst_node import *


class Sinewave(threading.Thread):
    def __init__(self, reader, node, method, args):
        threading.Thread.__init__(self)
        self.reader = reader
        self.args = args
        self.exitFlag = 0
        self.node = node
        self.method = method

    def stop(self):
        self.exitFlag = 1

    def run(self):
        count = 0
        while not self.exitFlag:
            count += 0.001
            count = count % 100
            value = (((math.sin(count) + 1) * 0.2) + 0.3) * 127
            self.args["value"] = value
            self.reader.update_remote_method(self.node.methods[self.method], self.args)
            time.sleep(0.001)

reader = ZstNode("SinewaveWriter", sys.argv[1])
reader.start()

nodeList = reader.request_node_peerlinks()

print "Nodes on stage:"
print "---------------"
for name, peer in nodeList.iteritems():
    print name, json.dumps(peer.as_dict(), indent=1, sort_keys=True)
print ""

nodeName = str(raw_input("Enter a node to connect to: "))
methodName = str(raw_input("Enter a method to control: "))

if nodeName in nodeList:
    node = nodeList[nodeName]
    reader.subscribe_to(node)
    reader.connect_to_peer(node)

    time.sleep(1)
    #reader.handle_requests()

    #args = {'deviceindex': 0, 'trackindex': 4, 'parameterindex': 1, 'value': value}
    args = {}
    for argname, argvalue in node.methods[methodName].args.iteritems():
        args[argname] = raw_input("Enter a value for the argument " + str(argname) + ": ")

    sinewave = Sinewave(reader, node, methodName, args)
    sinewave.start()
    try:
        while True:
            time.sleep(2)
    except KeyboardInterrupt:
        sinewave.stop()
        sinewave.join(2)
        reader.stop()
        reader.join(2)
        print "\nExiting..."
