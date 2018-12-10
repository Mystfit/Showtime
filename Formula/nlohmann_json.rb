class NlohmannJson < Formula
  desc "JSON for Modern C++"
  homepage "https://github.com/nlohmann/json"
  url "https://github.com/nlohmann/json/archive/v3.4.0.tar.gz"
  sha256 "c377963a95989270c943d522bfefe7b889ef5ed0e1e15d535fd6f6f16ed70732"
  head "https://github.com/nlohmann/json.git", :branch => "develop"

  depends_on "cmake" => [:optional]

  def install
    mkdir "build" do
      system "cmake", "..", "-DJSON_BuildTests=OFF", *std_cmake_args
      system "make", "install"
    end
  end

  def caveats
    <<~EOS
      If built with CMake support, you can use find_package to use the library.

      Without it, please set your include path accordingly:
      CPPFLAGS: -I#{include}
    EOS
  end

  test do
    (testpath/"test.cpp").write <<~EOS
      #include <nlohmann/json.hpp>

      using nlohmann::json;

      int main() {
        // create an empty structure (null)
        json j;

        // add a number that is stored as double (note the implicit conversion of j to an object)
        j["pi"] = 3.141;

        // add a Boolean that is stored as bool
        j["happy"] = true;

        // add a string that is stored as std::string
        j["name"] = "Niels";

        // add another null object by passing nullptr
        j["nothing"] = nullptr;

        // add an object inside the object
        j["answer"]["everything"] = 42;

        // add an array that is stored as std::vector (using an initializer list)
        j["list"] = { 1, 0, 2 };

        // add another object (using an initializer list of pairs)
        j["object"] = { {"currency", "USD"}, {"value", 42.99} };

        // instead, you could also write (which looks very similar to the JSON above)
        json j2 = {
          {"pi", 3.141},
          {"happy", true},
          {"name", "Niels"},
          {"nothing", nullptr},
          {"answer", {
            {"everything", 42}
          }},
          {"list", {1, 0, 2}},
          {"object", {
            {"currency", "USD"},
            {"value", 42.99}
          }}
        };

        // a user-defined literal
        json j3 = R"(
          {
            "pi": 3.141,
            "happy": true,
            "name": "Niels",
            "nothing": null,
            "answer": {
              "everything": 42
            },
            "list": [1, 0, 2],
            "object": {
              "currency": "USD",
              "value": 42.99
            }
          }
        )"_json;
      }
    EOS
    system ENV.cxx, "test.cpp", "-I#{include}", "-std=c++11", "-o", "test"
    system "./test"
  end
end
