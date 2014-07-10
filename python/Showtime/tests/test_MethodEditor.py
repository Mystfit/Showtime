import sys
import time
from Showtime.zst_node import *

reader = ZstNode("MethodEditor", sys.argv[1])
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

    count = 0
    try:
        while True:
            args = {}
            if len(node.methods[methodName].args) > 0:
                for argname, argvalue in node.methods[methodName].args.iteritems():
                    args[argname] = raw_input("Enter a value for the argument " + str(argname) + ": ")
            reader.update_remote_method(node.methods[methodName], args)
            time.sleep(1)
    except KeyboardInterrupt:
        reader.close()
        print "Exiting"
