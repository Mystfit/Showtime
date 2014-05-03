using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ZST {
	public class ZstMethod {
		public static string READ = "read";
		public static string WRITE = "write";
        public static string RESPONDER = "responder";
		public static string METHOD_LIST = "zst_method_list";
		public static string METHOD_NAME = "zst_method_name";
		public static string METHOD_ORIGIN_NODE = "zst_method_orig";
		public static string METHOD_ARGS = "zst_method_args";
		public static string METHOD_ACCESSMODE = "zst_method_Accessmode";
		public static string METHOD_OUTPUT = "zst_method_output";


        /// <summary>Get the name of this node</summary>
        public string name { get { return m_name; } }
        protected string m_name;

        /// <summary>Get the owner of this method</summary>
        public string node { get { return m_node; } }
		protected string m_node;

        /// <summary>Get the access mode of this method</summary>
        public string accessMode { get { return m_accessMode; } }
		protected string m_accessMode;

        /// <summary> Get the arguments for this method</summary>
        public Dictionary<string, object> args { get { return m_args; } }
        protected Dictionary<string, object> m_args;

        /// <summary>Get the stored output value of this method</summary>
        public object output
        {
            get { return m_output; }
            set { m_output = value; }
        }
        protected object m_output;

        /// <summary>Reference to the actual method this wrapper controls</summary>
        protected Func<ZstMethod, object> m_callback;
        public Func<ZstMethod, object> callback { 
            get { return m_callback; } 
            set { m_callback = value; } 
        }

        // Constructors
        // ------------
        public ZstMethod(string name, string node)
        {
            init(name, node, READ, null, null);
        }
        public ZstMethod(string name, string node, string accessMode, Dictionary<string, object> args)
        {
			init(name, node, accessMode, args, null);
		}
        public ZstMethod(string name, string node, string accessMode, Dictionary<string, object> args, Func<ZstMethod, object> callback)
        {
			init(name, node, accessMode, args, callback);
		}

        private void init(string name, string node, string accessMode, Dictionary<string, object> args, Func<ZstMethod, object> callback)
        {
			m_name = name;
			m_node = node;
			m_accessMode = accessMode;
			m_callback = callback;
			m_output = "";
            
            if (args != null)
                m_args = args;
            else
                m_args = new Dictionary<string, object>();
		}

        /// <summary>
        /// Run the local callback for this method
        /// </summary>
        public object run(ZstMethod methodData)
        {
			try{
                return m_callback(methodData);
			} catch(Exception e){
				// Handle callback error here
			}
			return null;
		}


        /// <summary>
        /// Returns this class as a dictionary.
        /// </summary>
		public Dictionary<string, object> as_dict()
        {
			Dictionary<string, object> outDict = new Dictionary<string, object>();
			
			return new Dictionary<string, object>(){
				{METHOD_NAME, m_name},
				{METHOD_ORIGIN_NODE, m_node},
				{METHOD_ACCESSMODE, m_accessMode},
				{METHOD_ARGS, m_args},
				{METHOD_OUTPUT, m_output}
			};
		}

        /// <summary>
        /// Compare two argument lists and make sure they match.
        /// </summary>
		public static bool compareArgLists(Dictionary<string, object> args1, Dictionary<string, object> args2)
        {
            if (args1 == null && args2 == null)
                return true;

            if (args1 != null)
            {
                foreach (string arg in args1.Keys)
                {
                    if (args2 != null)
                    {
                        if (!args2.Keys.Contains(arg))
                            return false;
                    }
                }
                return true;
            }

            return false;
		}


        /// <summary>
        /// Build local ZstMethods from a dictionary list.
        /// </summary>
		public static Dictionary<string, ZstMethod> buildLocalMethods(Dictionary<string, object> methods)
        {
			Dictionary<string, ZstMethod> outMethods = new Dictionary<string, ZstMethod>();

			foreach(KeyValuePair<string, object> method in methods){
                Dictionary<string, object> methodDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(method.Value.ToString());
                outMethods[method.Key] = dictToZstMethod(methodDict);
			}

			return outMethods;
		}


        /// <summary>
        /// Converts a dictionary to a ZstMethod
        /// </summary>
        public static ZstMethod dictToZstMethod(Dictionary<string, object> method){

            if (method.Count < 1)
                return null;

            Dictionary<string, object> methodArgs = null;

            if (method[METHOD_ARGS] != null)
                methodArgs = JsonConvert.DeserializeObject<Dictionary<string, object>>(method[METHOD_ARGS].ToString());

			ZstMethod localMethod = new ZstMethod(
                (string)method[METHOD_NAME],
                (string)method[METHOD_ORIGIN_NODE],
                (string)method[METHOD_ACCESSMODE],
                methodArgs
			);
            localMethod.output = method[METHOD_OUTPUT];

            return localMethod;
        }
	}
}
