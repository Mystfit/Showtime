import sys
import time
from zst_node import *

def echo_position(methodData):
    print methodData.output

if len(sys.argv) > 2:
    node = ZstNode(sys.argv[1], sys.argv[2])
    node.start()
    nodeList = node.request_node_peerlinks()

    liveNode = nodeList["LiveNode"]
    node.subscribe_to(liveNode)
    node.connect_to_peer(liveNode)
    node.subscribe_to_method(liveNode.methods["send_updated"], echo_position)

    try:
        while True:
            time.sleep(1)
    except KeyboardInterrupt:
        node.close()
        print "Finished"

else:
    print "Please provide a name for this node!"
