import sys
import math
import time
from zst_node import *

reader = ZstNode("Reader", sys.argv[1])
nodeList = reader.request_node_peerlinks()

print "Nodes on stage:"
print "---------------"
for name, peer in nodeList.iteritems():
    print name, json.dumps(peer.as_dict(), indent=1, sort_keys=True)
print ""

nodeName = str(raw_input("Enter a node to connect to: "))
methodName = str(raw_input("Enter a method to control: "))



# nodeName = "SineWaveGenerator"
# methodName = "set_speed"
# methodArg = "speed"
# val = 1

if nodeName in nodeList:
    node = nodeList[nodeName]
    reader.subscribe_to(node)

    socket = reader.ctx.socket(zmq.REQ)
    socket.connect(node.replyAddress)
    reader.request_register_node(socket)

    time.sleep(1)
    reader.handle_requests()

    count = 0
    while True:
        args = {}
        for argname, argvalue in node.methods[methodName].args.iteritems():
            args[argname] = raw_input("Enter a value for the argument " + str(argname) + ": ")
        reader.update_remote_method(node.methods[methodName], args)
        time.sleep(0.1)
