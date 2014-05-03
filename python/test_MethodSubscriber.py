import sys
import time
from zst_node import *

class TestClass:
    def __init__(self):
        self.valueChangedCount = 0

    def incrementValueChanged(self, methodData):
        self.valueChangedCount += 1


testClass = TestClass()

if len(sys.argv) > 2:
    node = ZstNode(sys.argv[1], sys.argv[2])
    node.start()
    nodeList = node.request_node_peerlinks()

    liveNode = nodeList["LiveNode"]
    node.subscribe_to(liveNode)
    node.connect_to_peer(liveNode)
    node.subscribe_to_method(liveNode.methods["value_updated"], testClass.incrementValueChanged)

    try:
        while True:
            time.sleep(1)
            print testClass.valueChangedCount
    except KeyboardInterrupt:
        node.close()
        print "Finished"

else:
    print "Please provide a name for this node!"