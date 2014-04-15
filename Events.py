import json


class KeyMessage():

    def __init__(self, key, action, data):
        self.key = key
        self.action = action
        self.data = data


# Base Event Class
# ----------------
# Allows for sending of keyaddress, action, data event messages
class Event():
    @staticmethod
    def send(socket, key, action, data=None):
        socket.send_multipart([key, action, json.dumps(data)])

    @staticmethod
    def recv(socket):
        msg = socket.recv_multipart()
        key, action, data = msg
        key = key if key else None
        action = action if action else None
        data = json.loads(data) if data else None
        return KeyMessage(key=key, action=action, data=data)


# Stage Event Class
# -------------------------------------------
# Handles messages sent from the stage to equipment/performers
class StageEvent(Event):

    # Static stage address key
    STAGE_KEY = "zst_stage"

    # Event action types
    OK = "OK"
    NO_EQUIPMENT_FOUND = "stage_no_equipment"
    EQUIPMENT_LIST_UPDATED = "stage_eq_list_update"
    EQUIPMENT_VALUE_UPDATED = "stage_eq_val_update"

    # Event methods
    @staticmethod
    def ok(socket):
        Event.send(socket, StageEvent.STAGE_KEY, StageEvent.OK)

    @staticmethod
    def equipment_not_found(socket, equipmentName):
        Event.send(
            socket, StageEvent.STAGE_KEY, StageEvent.NO_EQUIPMENT_FOUND,
            {EquipmentEvent.KEY_EQ_NAME: equipmentName})


# Equipment Event Class
# -------------------------------------------
# Handles messages sent from equipment to the stage
class EquipmentEvent(Event):

    # Static equipment address key
    EQUIPMENT_KEY = "zst_equipment"

    # Event action types
    REGISTER_ADDRESS = "eq_register_address"
    REGISTER_ABILITY = "eq_register_ability"
    REMOVE_ABILITY = "eq_remove_ability"
    UPDATE_VALUES = "eq_UPDATE_VALUES"

    # Data keys
    KEY_EQ_ABILITYKEY = "eqdata_abilitykey"
    KEY_EQ_NAME = "eqdata_name"
    KEY_EQ_ADDRESS = "eqdata_address"
    KEY_EQ_DATA = "eqdata_data"
    KEY_EQ_VALUE = "eqdata_value"

    # Event methods
    @staticmethod
    def request_register_address(socket, equipmentName, address):
        Event.send(
            socket, EquipmentEvent.EQUIPMENT_KEY, EquipmentEvent.REGISTER_ADDRESS,
            {EquipmentEvent.KEY_EQ_NAME: equipmentName, EquipmentEvent.KEY_EQ_ADDRESS: address})
        return Event.recv(socket)

    @staticmethod
    def register_ability(socket, abilitykey, abilitydata):
        Event.send(socket, abilitykey, EquipmentEvent.REGISTER_ABILITY, abilitydata)

    @staticmethod
    def remove_ability(socket, abilitykey):
        Event.send(socket, abilitykey, EquipmentEvent.REMOVE_ABILITY)

    @staticmethod
    def update_values(socket, abilitykey, value):
        Event.send(socket, abilitykey, EquipmentEvent.UPDATE_VALUES, value)

    # Helper methods
    @staticmethod
    def get_equip_name_from_key(key):
        return key.split("/")[0]


# Performer Event Class
# -------------------------------------------
# Handles messages sent from a performer to equipment and the stage
class PerformerEvent(Event):

    # Static performer address key
    PERFORMER_KEY = "zst_performer"

    # Event types
    REQUEST_EQUIPMENT_LIST = "REQUEST_EQUIPMENT_LIST"
    REQUEST_EQUIPMENT_VALUES = "REQUEST_EQUIPMENT_VALUES"
    UPDATE_VALUES = "perf_update_values"

    # Event methods
    @staticmethod
    def request_equipment_list(socket):
        Event.send(socket, PerformerEvent.PERFORMER_KEY, PerformerEvent.REQUEST_EQUIPMENT_LIST)

    @staticmethod
    def update_values(socket, abilitykey, value):
        Event.send(socket, abilitykey, PerformerEvent.UPDATE_VALUES, value)
