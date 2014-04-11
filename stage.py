# ZmqShowtime Stage Server
# 
# The stage server needs to acomplish a few tasks. Firstly, it needs
# to act as destination for new Equipment that is adding itself to the
# the performance and maintain an list for performers to bind themselves.
#
# Secondly, it needs to act as a proxy for published messages from 
# Performers/Equipment and forward pub/sub messages to interested parties.

import zmq

version = 0.1

class StageConstants():
    EQUIPMENT_ENTRY_PORT = 6000
    EQUIPMENT_SUBSCRIBER_PORT = 6001
    EQUIPMENT_PUBLISHER_PORT = 6002
    STAGE_ADDRESS = "localhost"

class Stage():
    def __init__(self):
        # Zmq sockets
        ctx = zmq.Context()
        self.equipmentStateUpdates  = ctx.socket(zmq.REP)
        self.equipmentStateUpdates.bind('tcp://*:' + str(StageConstants.EQUIPMENT_ENTRY_PORT))
        self.subscribeEquipmentValues = ctx.socket(zmq.SUB)
        self.subscribeEquipmentValues.bind('tcp://*:' + str(StageConstants.EQUIPMENT_SUBSCRIBER_PORT))
        self.publishEquipmentValues = ctx.socket(zmq.PUB)
        self.publishEquipmentValues.bind('tcp://*:' + str(StageConstants.EQUIPMENT_PUBLISHER_PORT))


    def listen(self):
        # Initialize poll set
        poller = zmq.Poller()
        poller.register(self.equipmentStateUpdates, zmq.POLLIN)
        poller.register(self.subscribeEquipmentValues, zmq.POLLIN)
        poller.register(self.publishEquipmentValues, zmq.POLLIN)

        # Process messages from both sockets
        while True:
            try:
                socks = dict(poller.poll())
            except KeyboardInterrupt:
                print "\nFinished"
                break

            if self.equipmentStateUpdates in socks:
                message = self.equipmentStateUpdates.recv()
                self.equipmentStateUpdates.send(str(message))
                print("Received new equipment state: %s" % message)

            if self.subscribeEquipmentValues in socks:
                message = self.subscribeEquipmentValues.recv()
                self.save_equipment_values(message)
                self.publishEquipmentValues.send(message)

    def save_equipment_values(self, message):
        # Save equipment values locally so we have an up to date state
        print("Forwarding received request to subscribers: %s" % message)



def main():
    print "ZmqShowtime version %s" % version
    stage = Stage()
    stage.listen()

if __name__ == '__main__':
    main()