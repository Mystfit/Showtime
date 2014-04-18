class ZstMethod():

    # Message constants
    READ = "read"
    WRITE = "write"
    METHOD_LIST = "zst_method_list"
    METHOD_NAME = "zst_method_name"
    METHOD_ORIGIN_NODE = "zst_method_orig"
    METHOD_ARGS = "zst_method_args"
    METHOD_ACCESSMODE = "zst_method_Accessmode"
    METHOD_OUTPUT = "zst_method_output"

    def __init__(self, name, node, accessMode, args, output=None):
        self.name = name
        self.node = node
        self.accessMode = accessMode
        self.args = args
        self.output = output

    def as_dict(self):
        return {
            ZstMethod.METHOD_NAME: self.name,
            ZstMethod.METHOD_ORIGIN_NODE: self.node,
            ZstMethod.METHOD_ACCESSMODE: self.accessMode,
            ZstMethod.METHOD_ARGS: self.args,
            ZstMethod.METHOD_OUTPUT: self.output}

    @staticmethod
    def build_local_methods(methods):
        methodList = {}
        for methodname, method in methods.iteritems():
            localMethod = ZstMethod(
                method[ZstMethod.METHOD_NAME],
                method[ZstMethod.METHOD_ORIGIN_NODE],
                method[ZstMethod.METHOD_ACCESSMODE],
                method[ZstMethod.METHOD_ARGS],
                method[ZstMethod.METHOD_OUTPUT])
            methodList[methodname] = localMethod
        return methodList
