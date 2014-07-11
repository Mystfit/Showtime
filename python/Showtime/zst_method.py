class ZstMethod():

    # Message constants
    READ = "read"
    WRITE = "write"
    RESPONDER = "responder"
    METHOD_LIST = "zst_method_list"
    METHOD_NAME = "zst_method_name"
    METHOD_ORIGIN_NODE = "zst_method_orig"
    METHOD_ARGS = "zst_method_args"
    METHOD_ACCESSMODE = "zst_method_Accessmode"
    METHOD_OUTPUT = "zst_method_output"

    def __init__(self, name, node, accessMode=None, args=None, callback=None, output=None):
        self.name = name
        self.node = node
        self.accessMode = accessMode
        self.args = args
        self.output = output
        self.callback = callback

    def run(self, args):
        if not self.callback:
            print "No external callback set for this method object!"
            return
        return self.callback(args)

    def as_dict(self):
        return {
            ZstMethod.METHOD_NAME: self.name,
            ZstMethod.METHOD_ORIGIN_NODE: self.node,
            ZstMethod.METHOD_ACCESSMODE: self.accessMode,
            ZstMethod.METHOD_ARGS: self.args,
            ZstMethod.METHOD_OUTPUT: self.output}

    def set_Args(self, args):
        for name, value in args.iteritems():
            if name in self.args:
                self.args[name] = value

    @staticmethod
    def compare_arg_lists(args1, args2):
        if not args1 and not args2:
            return True
        for name, value in args1.iteritems():
            if name not in args2:
                return False
        return True

    @staticmethod
    def build_local_methods(methods):
        methodList = {}
        for methodname, method in methods.iteritems():
            localMethod = ZstMethod(
                name=method[ZstMethod.METHOD_NAME],
                node=method[ZstMethod.METHOD_ORIGIN_NODE],
                accessMode=method[ZstMethod.METHOD_ACCESSMODE],
                args=method[ZstMethod.METHOD_ARGS],
                output=method[ZstMethod.METHOD_OUTPUT])
            methodList[methodname] = localMethod
        return methodList
