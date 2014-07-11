import sys
import time
from Showtime.zst_node import *


def echo_method(methodData):
    print("Method: " + str(methodData.name) + " Arguments: " +
          str(methodData.args) + " Output: " + str(methodData.output))

subscriber = ZstNode("MethodSubscriber", sys.argv[1])
subscriber.start()
nodeList = subscriber.request_node_peerlinks()

print "Nodes on stage:"
print "---------------"
for name, peer in nodeList.iteritems():
    print name, json.dumps(peer.as_dict(), indent=1, sort_keys=True)
print ""

nodeName = str(raw_input("Enter a node to connect to: "))
methodName = str(raw_input("Enter a method to subscribe to: "))

if nodeName in nodeList:
    node = nodeList[nodeName]
    subscriber.subscribe_to(node)
    subscriber.connect_to_peer(node)
    subscriber.subscribe_to_method(node.methods[methodName], echo_method)
    subscriber.listen()
try:
    while True:
        time.sleep(1)
except KeyboardInterrupt:
    subscriber.close()
    print "Finished"
