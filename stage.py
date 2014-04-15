# ZmqShowtime Stage Server
#
# The stage server needs to acomplish a few tasks. Firstly, it needs
# to act as destination for new Equipment that is adding itself to the
# the performance and maintain an list for performers to bind themselves.
#
# Secondly, it needs to act as a proxy for published messages from
# Performers/Equipment and forward pub/sub messages to interested parties.

import zmq
from Events import *
from equipment import EquipmentRef

version = 0.1


class StageConstants():
    EQUIPMENT_ENTRY_PORT = 6000
    EQUIPMENT_SUBSCRIBER_PORT = 6001
    EQUIPMENT_PUBLISHER_PORT = 6002
    EQUIPMENT_START_PORT = 6003
    STAGE_ADDRESS = "localhost"


class Stage():

    def __init__(self):
        # Zmq sockets
        ctx = zmq.Context()
        self.equipmentStateUpdates = ctx.socket(zmq.REP)
        self.equipmentStateUpdates.bind(
            'tcp://*:' + str(StageConstants.EQUIPMENT_ENTRY_PORT))

        self.subscribeEquipmentValues = ctx.socket(zmq.SUB)
        self.subscribeEquipmentValues.bind(
            'tcp://*:' + str(StageConstants.EQUIPMENT_SUBSCRIBER_PORT))

        # Subscribe to all incoming messages on this socket
        self.subscribeEquipmentValues.setsockopt(zmq.SUBSCRIBE, '')

        self.publishEquipmentValues = ctx.socket(zmq.PUB)
        self.publishEquipmentValues.bind(
            'tcp://*:' + str(StageConstants.EQUIPMENT_PUBLISHER_PORT))

        # Stage objects
        self.equipment = {}

    def listen(self):
        # Initialize poll set
        poller = zmq.Poller()
        poller.register(self.equipmentStateUpdates, zmq.POLLIN)
        poller.register(self.subscribeEquipmentValues, zmq.POLLIN)
        poller.register(self.publishEquipmentValues, zmq.POLLIN)

        # Process messages from all sockets
        while True:
            try:
                socks = dict(poller.poll())
            except KeyboardInterrupt:
                print "\nFinished"
                break

            # Handle equipment state updates
            if self.equipmentStateUpdates in socks:
                message = Event.recv(self.equipmentStateUpdates)

                # The equipment requested an address bind.
                if message.action == EquipmentEvent.REGISTER_ADDRESS:
                    self.process_equipment_registrations(message)

            # Handle equipment value updates
            if self.subscribeEquipmentValues in socks:

                message = Event.recv(self.subscribeEquipmentValues)

                #Equipment is registering an ability to the stage
                if message.action == EquipmentEvent.REGISTER_ABILITY:
                    self.process_ability_registrations(message)

                if message.action == EquipmentEvent.REMOVE_ABILITY:
                    self.process_equipment_removals(message)

                if message.action == EquipmentEvent.UPDATE_VALUES:
                    self.process_equipment_updates(message)

    def process_equipment_registrations(self, message):
        equipName = message.data[EquipmentEvent.KEY_EQ_NAME]
        if equipName in self.equipment:
            print "Overriding existing equipment: " + equipName

        # Create a reference piece of equipment to sync equipment abilities and
        # values
        self.equipment[equipName] = EquipmentRef(equipName)
        self.equipment[equipName].bindAddress(
            message.data[EquipmentEvent.KEY_EQ_ADDRESS])

        # Send an ACK back to the equipment letting it know we've completed the
        # address bind.
        StageEvent.ok(self.equipmentStateUpdates)
        print "Equipment \'" + equipName + "\' confirming it has bound address " + str(message.data[EquipmentEvent.KEY_EQ_ADDRESS])            

    def process_ability_registrations(self, message):
        equipname = EquipmentEvent.get_equip_name_from_key(message.key)
        if equipname in self.equipment:
            ability = self.equipment[equipname].addAbility(message.key, message.data)
            print "Registered ability '{0}'' as type '{1}'".format(ability.key, ability.type)
        else:
            print "{0} not found in equipment list! Has it been registered?".format(equipname)

    def process_equipment_removals(self, message):
        pass

    def process_equipment_updates(self, message):
        pass
    

if __name__ == '__main__':
    print "ZmqShowtime version %s" % version
    stage = Stage()
    stage.listen()
