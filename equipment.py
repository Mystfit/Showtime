import zmq
import uuid
import socket
import timeit
from Events import *
from Abilities import *


# Reference Equipment class.
#
# Template equipment that lives on the stage server and holds all
# information about a piece of registered equipment

class EquipmentRef():

    def __init__(self, name):
        self.address = None
        self.name = name
        self.abilities = {}

    def addAbility(self, key, abilitydata):
        ability = None
        if(abilitydata[BaseAbility.KEY_ABL_TYPE] == "unit"):
            ability = UnitAbility(
                key, abilitydata[UnitAbility.KEY_ABL_MIN], abilitydata[UnitAbility.KEY_ABL_MAX], abilitydata[UnitAbility.KEY_ABL_UNIT])
        elif(abilitydata[BaseAbility.KEY_ABL_TYPE] == "toggle"):
            ability = ToggleAbility(key)
        elif(abilitydata[BaseAbility.KEY_ABL_TYPE] == "base"):
            ability = BaseAbility(key)
        self.abilities[key] = ability
        return ability

    def bindAddress(self, port):
        self.port = port


class Equipment():

    def __init__(self, name):
        self.name = name
        self.address = None

        self.create_sockets()
        #print "Registered in %ss" % str(timeit.Timer(self.register_to_Stage).timeit(number=1))
        self.register_to_stage()

    def create_sockets(self):
        # ZMQ sockets
        ctx = zmq.Context()
        self.stageRequestSocket = ctx.socket(zmq.REQ)
        self.stageRequestSocket.connect('tcp://localhost:6000')

        self.publishEquipmentValues = ctx.socket(zmq.PUB)
        self.publishEquipmentValues.connect("tcp://localhost:6001")

        host_ip = socket.gethostbyname(socket.gethostname())
        address = 'tcp://%s:*' % host_ip

        self.subscribeEquipmentValues = ctx.socket(zmq.SUB)
        self.subscribeEquipmentValues.bind(address)
        self.address = self.subscribeEquipmentValues.getsockopt(zmq.LAST_ENDPOINT)

         # Initialize poll set
        self.poller = zmq.Poller()
        self.poller.register(self.subscribeEquipmentValues, zmq.POLLIN)

    def listen(self):
        while True:
            try:
                self.process_messages()
            except KeyboardInterrupt:
                print "\nFinished"
                break

    # Event loop
    def process_messages(self):
        socks = dict(self.poller.poll())

        # Handle equipment state updates
        if self.subscribeEquipmentValues in socks:
            message = Event.recv(self.subscribeEquipmentValues)

            # The equipment requested an address bind.
            if message.action == EquipmentEvent.REGISTER_ADDRESS:
                self.process_equipment_registrations(message)

    def listen_for_incoming(message):
        print message

    def register_to_stage(self):
        # Request a port for this equipment to bind as its outgoing sender
        print "Requesting stage registers our address as " + self.address
        message = EquipmentEvent.request_register_address(
            self.stageRequestSocket, self.name, self.address)

        if message.key == StageEvent.OK:
            print "Stage acknowledged our address as " + self.address

    def register_ability(self, ability):
        print "Registering %s with stage." % ability.key
        EquipmentEvent.register_ability(
            self.publishEquipmentValues, ability.key, ability.as_dict())


# Since we shouldn't call the base equipment straight from a standalone 
# python session, we can use the main() function to run test cases    
if __name__ == '__main__':
    equipment = Equipment("bagpipes")
    # Create some test abilities
    testAbility1 = UnitAbility(equipment.name + "/scalartwibble")
    testAbility2 = ToggleAbility(equipment.name + "/buttonbloop")
    testAbility3 = BaseAbility(equipment.name + "/generictwatter")

    # Register abilities with stage
    equipment.register_ability(testAbility1)
    equipment.register_ability(testAbility2)
    equipment.register_ability(testAbility3)

    # Enter listening loop
    equipment.listen()
