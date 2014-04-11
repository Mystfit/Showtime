from stage import StageConstants
from stage import Stage
import time
import simplejson as json
import zmq

def main():
    ctx = zmq.Context()
    equipmentInsert = ctx.socket(zmq.REQ)
    equipmentInsert.connect('tcp://' + StageConstants.STAGE_ADDRESS + ':' + str(StageConstants.EQUIPMENT_ENTRY_PORT))

    equipmentInsert.send('{"name":"byron"}')
    message = equipmentInsert.recv()
    print json.loads(str(message))
    time.sleep(1)

main()