import sys
import math
import time
from zst_node import *

reader = ZstNode("MethodEditor", sys.argv[1])
nodeList = reader.request_node_peerlinks()

print "Nodes on stage:"
print "---------------"
for name, peer in nodeList.iteritems():
    print name, json.dumps(peer.as_dict(), indent=1, sort_keys=True)
print ""

node = nodeList["LiveNode"]
reader.subscribe_to(node)
reader.connect_to_peer(node)

time.sleep(1)

count = 0
try:
    print reader.echo_remote_method(node.methods["get_song_layout"], None)
except KeyboardInterrupt:
    print "Exiting"
    reader.close()
