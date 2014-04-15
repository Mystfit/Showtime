from Events import *
import json

class BaseAbility():
    KEY_ABL_VALUE = "abl_value"
    KEY_ABL_TYPE = "abl_type"
    KEY_ABL_TYPE = "base"

    def __init__(self, key, abilitytype=None, parent=None):
        self.key = key
        self.parent = parent
        self.value = float(0)
        self.type = abilitytype if abilitytype else BaseAbility.KEY_ABL_TYPE

    def as_dict(self, obj=None):
        if not obj:
            obj = {
                BaseAbility.KEY_ABL_TYPE: self.type,
                EquipmentEvent.KEY_EQ_VALUE: self.value}
        else:
            obj[BaseAbility.KEY_ABL_TYPE] = self.type
            obj[EquipmentEvent.KEY_EQ_VALUE] = self.value

        return obj


class UnitAbility(BaseAbility):
    KEY_ABL_MIN = "abl_min"
    KEY_ABL_MAX = "abl_max"
    KEY_ABL_UNIT = "abl_unit"
    KEY_ABL_TYPE = "unit"

    def __init__(self, key, abilitytype=None, minVal=0, maxVal=1, units=None, parent=None):
        abilitytype = abilitytype if abilitytype else UnitAbility.KEY_ABL_TYPE
        BaseAbility.__init__(self, key, abilitytype, parent)
        self.min = minVal
        self.max = maxVal
        self.units = units

    def set_range(self, minVal, maxVal):
        self.min = minVal
        self.max = maxVal

    def normalized():
        return (self.max - self.min) * (self.value - self.min) / (self.max - self.min)

    def as_dict(self, obj=None):
        if not obj:
            obj = {
                UnitAbility.KEY_ABL_MIN: self.min,
                UnitAbility.KEY_ABL_MAX: self.max,
                UnitAbility.KEY_ABL_UNIT: self.units}
        else:
                obj[UnitAbility.KEY_ABL_MIN] = self.min
                obj[UnitAbility.KEY_ABL_MAX] = self.max
                obj[UnitAbility.KEY_ABL_UNIT] = self.units

        return BaseAbility.as_dict(self, obj)


class ToggleAbility(UnitAbility):
    KEY_ABL_TYPE = "toggle"

    def __init__(self, key, abilitytype=None, parent=None):
        abilitytype = abilitytype if abilitytype else ToggleAbility.KEY_ABL_TYPE
        UnitAbility.__init__(self, key, abilitytype, 0, 1, None, parent)

    def toggle_on():
        self.value = self.maxVal

    def toggle_off():
        self.value = self.minVal
