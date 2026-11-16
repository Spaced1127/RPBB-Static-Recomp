// Rocket Power Recomp - offline controller regression matrix.
//
// This test is owned by the RPBB tree so the release can be checked against the
// shared ModernGekko controller headers without depending on another project's
// test file. It uses the GUIDs and mapping strings from the SDL controller
// database that ships with ModernGekko (vendor/dolphin/Externals/SDL).
//
// Build/run: tools\build_controller_matrix.cmd
//
// Every row asserts the resolved layout, face-button swap and D-pad order, and
// then checks the generated GCPadNew.ini body. A standard Xbox pad must never
// resolve to the Switch layout, must never swap its D-pad, and must generate
// exactly the same profile as a PlayStation pad.

#include "controller_layout.hpp"
#include "frontend_config.hpp"

#include <chrono>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <iterator>
#include <string>
#include <string_view>

namespace fe = moderngekko::frontend;
namespace fs = std::filesystem;

namespace
{
int g_failures = 0;
int g_checks = 0;

void Check(bool condition, const std::string& label, const std::string& detail = {})
{
  ++g_checks;
  if (condition)
  {
    std::cout << "PASS " << label << '\n';
    return;
  }
  ++g_failures;
  std::cout << "FAIL " << label;
  if (!detail.empty())
    std::cout << " (" << detail << ')';
  std::cout << '\n';
}

std::string Describe(const fe::ControllerDetection& d)
{
  return "layout=" + std::string(fe::ControllerLayoutName(d.layout)) +
         " source=" + fe::ControllerDetectSourceName(d.source) +
         " dpad_swap=" + (d.dpad_swap ? "yes" : "no") +
         " face_swap=" + (d.face_swap ? "yes" : "no") +
         " paddles=" + (d.paddles ? "yes" : "no") +
         " family=" + d.family;
}

struct Expected
{
  fe::ControllerLayout layout;
  bool face_swap = false;
  bool dpad_swap = false;
  bool paddles = false;
  const char* family = nullptr;
  const char* source = nullptr;  // nullptr: any source
};

void CheckPad(const std::string& label, const std::string& guid,
              const std::string& mapping, SDL_GamepadType type,
              const Expected& expected)
{
  const fe::ControllerDetection d = fe::DetectControllerMapping(type, guid, mapping);
  const bool ok = d.layout == expected.layout && d.face_swap == expected.face_swap &&
                  d.dpad_swap == expected.dpad_swap && d.paddles == expected.paddles &&
                  (expected.family == nullptr || d.family == expected.family) &&
                  (expected.source == nullptr ||
                   std::string_view(fe::ControllerDetectSourceName(d.source)) ==
                       expected.source);
  Check(ok, label, Describe(d));
}

std::string GenerateProfile(const std::string& tag, fe::ControllerLayout layout,
                            bool dpad_swap = false)
{
  const fs::path directory =
      fs::temp_directory_path() /
      ("rpbb-controller-matrix-" + tag + "-" +
       std::to_string(std::chrono::steady_clock::now().time_since_epoch().count()));
  std::string message;
  if (!fe::GenerateControllerConfig(directory, std::string("SDL/0/Test Controller"),
                                    layout, dpad_swap, &message))
  {
    std::cout << "FAIL generate profile " << tag << " (" << message << ")\n";
    ++g_failures;
    ++g_checks;
    return {};
  }
  std::string generated;
  {
    std::ifstream input(directory / "Config" / "GCPadNew.ini");
    generated.assign(std::istreambuf_iterator<char>(input),
                     std::istreambuf_iterator<char>());
  }
  std::error_code ec;
  fs::remove_all(directory, ec);
  return generated;
}

bool Has(const std::string& text, std::string_view needle)
{
  return text.find(needle) != std::string::npos;
}
}  // namespace

int main()
{
  // SDL's own "xinput" mapping. This is what a wired Xbox pad gets on Windows
  // when SDL talks to it through XInput.
  const std::string xinput_mapping =
      "a:b0,b:b1,back:b6,dpdown:h0.4,dpleft:h0.8,dpright:h0.2,dpup:h0.1,"
      "guide:b10,leftshoulder:b4,leftstick:b8,lefttrigger:a2,leftx:a0,lefty:a1,"
      "rightshoulder:b5,rightstick:b9,righttrigger:a5,rightx:a3,righty:a4,"
      "start:b7,x:b2,y:b3,";

  // SDL's raw Xbox One mapping uses buttons for the D-pad. `dpleft` is a higher
  // button number than `dpright`, which is normal for that raw report.
  const std::string xbox_button_dpad =
      "a:b0,b:b1,back:b4,dpdown:b12,dpleft:b13,dpright:b14,dpup:b11,"
      "guide:b5,leftshoulder:b9,leftstick:b7,lefttrigger:a4,leftx:a0,lefty:a1,"
      "rightshoulder:b10,rightstick:b8,righttrigger:a5,rightx:a2,righty:a3,"
      "start:b6,x:b2,y:b3,";

  // A raw report whose left/right D-pad buttons are the other way round. It is
  // still a stock Xbox layout: button numbers carry no direction.
  const std::string xbox_button_dpad_high_left =
      "a:b0,b:b1,back:b4,dpdown:b12,dpleft:b13,dpright:b11,dpup:b14,"
      "guide:b5,leftshoulder:b9,leftstick:b7,lefttrigger:a4,leftx:a0,lefty:a1,"
      "rightshoulder:b10,rightstick:b8,righttrigger:a5,rightx:a2,righty:a3,"
      "start:b6,x:b2,y:b3,";

  const std::string dualsense_mapping =
      "a:b0,b:b1,back:b8,dpdown:h0.4,dpleft:h0.8,dpright:h0.2,dpup:h0.1,"
      "guide:b10,leftshoulder:b4,leftstick:b7,lefttrigger:a3,leftx:a0,lefty:a1,"
      "misc1:b12,paddle1:b14,paddle2:b13,paddle3:b16,paddle4:b15,"
      "rightshoulder:b5,rightstick:b8,righttrigger:a4,rightx:a2,righty:a5,"
      "start:b9,touchpad:b11,x:b2,y:b3,";

  const std::string pro2_nintendo =
      "a:b1,b:b0,x:b4,y:b3,back:b10,dpdown:h0.4,dpleft:h0.8,dpright:h0.2,"
      "dpup:h0.1,guide:b12,leftshoulder:b6,leftstick:b13,lefttrigger:b8,"
      "leftx:a0,lefty:a1,rightshoulder:b7,rightstick:b14,righttrigger:b9,"
      "rightx:a3,righty:a4,start:b11,";
  const std::string pro2_standard =
      "a:b0,b:b1,x:b3,y:b4,back:b10,dpdown:h0.4,dpleft:h0.8,dpright:h0.2,"
      "dpup:h0.1,start:b11,";
  const std::string pro2_switch =
      "a:b3,b:b2,x:b8,y:b7,back:b6,dpdown:b9,dpleft:b10,dpright:b11,"
      "dpup:b12,paddle1:b1,paddle2:b0,start:b5,";
  const std::string pro2_switch_reversed_hat =
      "a:b3,b:b2,x:b8,y:b7,back:b6,dpdown:b9,dpleft:h0.2,dpright:h0.8,"
      "dpup:b12,paddle1:b1,paddle2:b0,start:b5,";

  std::cout << "Xbox family (standard pads must stay Xbox)\n";
  CheckPad("Xbox 360 USB", "030000005e0400008e02000000000000", xinput_mapping,
           SDL_GAMEPAD_TYPE_XBOX360,
           {fe::ControllerLayout::Xbox, false, false, false, "Xbox 360", "guid"});
  CheckPad("Xbox 360 Bluetooth", "050000005e0400008e02000000783f00",
           xbox_button_dpad, SDL_GAMEPAD_TYPE_XBOX360,
           {fe::ControllerLayout::Xbox, false, false, false, "Xbox 360", "guid"});
  CheckPad("Xbox One (02D1)", "030000005e040000d102000000000000", xinput_mapping,
           SDL_GAMEPAD_TYPE_XBOXONE,
           {fe::ControllerLayout::Xbox, false, false, false, "Xbox One", "guid"});
  CheckPad("Xbox One (02DD)", "030000005e040000dd02000000000000", xinput_mapping,
           SDL_GAMEPAD_TYPE_XBOXONE,
           {fe::ControllerLayout::Xbox, false, false, false, "Xbox One", "guid"});
  CheckPad("Xbox One S (02E0) Bluetooth", "050000005e040000e002000000783f00",
           xbox_button_dpad, SDL_GAMEPAD_TYPE_XBOXONE,
           {fe::ControllerLayout::Xbox, false, false, false, "Xbox One S", "guid"});
  CheckPad("Xbox One (02FD) Bluetooth", "050000005e040000fd020000ffff3f00",
           xbox_button_dpad, SDL_GAMEPAD_TYPE_XBOXONE,
           {fe::ControllerLayout::Xbox, false, false, false, "Xbox One S", "guid"});
  CheckPad("Xbox Series X|S (0B12) USB", "030000005e040000120b000000000000",
           xinput_mapping, SDL_GAMEPAD_TYPE_XBOXONE,
           {fe::ControllerLayout::Xbox, false, false, false, "Xbox Series X|S",
            "guid"});
  CheckPad("Xbox Series X|S (0B12) Bluetooth",
           "050000005e040000120b000000783f80", xbox_button_dpad,
           SDL_GAMEPAD_TYPE_XBOXONE,
           {fe::ControllerLayout::Xbox, false, false, false, "Xbox Series X|S",
            "guid"});
  CheckPad("Xbox Series X|S (0B13) USB", "030000005e040000130b000000000000",
           xinput_mapping, SDL_GAMEPAD_TYPE_XBOXONE,
           {fe::ControllerLayout::Xbox, false, false, false, "Xbox Series X|S",
            "guid"});

  std::cout << "\nXbox D-pad is never inverted by button numbering\n";
  CheckPad("Xbox 360 with dpleft>b dpright", "030000005e0400008e02000000000000",
           xbox_button_dpad_high_left, SDL_GAMEPAD_TYPE_XBOX360,
           {fe::ControllerLayout::Xbox, false, false, false, "Xbox 360", "guid"});
  CheckPad("Unknown Microsoft PID with reversed raw order",
           "030000005e040000200b000011050000", xbox_button_dpad_high_left,
           SDL_GAMEPAD_TYPE_XBOXONE,
           {fe::ControllerLayout::Xbox, false, false, false, "Xbox gamepad",
            "guid-family"});
  CheckPad("Xbox 360 with a remapped a>b mapping",
           "030000005e0400008e02000000000000",
           "a:b1,b:b0,x:b2,y:b3,dpdown:h0.4,dpleft:h0.8,dpright:h0.2,"
           "dpup:h0.1,start:b7,",
           SDL_GAMEPAD_TYPE_XBOX360,
           {fe::ControllerLayout::Xbox, false, false, false, "Xbox 360", "guid"});

  std::cout << "\nXbox Elite Series 1/2\n";
  CheckPad("Elite Series 1 (02E3)", "030000005e040000e302000000000000",
           xbox_button_dpad, SDL_GAMEPAD_TYPE_XBOXONE,
           {fe::ControllerLayout::Xbox, false, false, true, "Xbox Elite Series 1",
            "guid"});
  CheckPad("Elite Series 2 (0B00) Bluetooth",
           "050000005e040000000b000000783f80", xbox_button_dpad,
           SDL_GAMEPAD_TYPE_XBOXONE,
           {fe::ControllerLayout::Xbox, false, false, true, "Xbox Elite Series 2",
            "guid"});
  CheckPad("Elite Series 2 (0B05) USB", "030000005e040000050b000003090000",
           xbox_button_dpad, SDL_GAMEPAD_TYPE_XBOXONE,
           {fe::ControllerLayout::Xbox, false, false, true, "Xbox Elite Series 2",
            "guid"});
  CheckPad("Elite Series 2 (0B05) Bluetooth",
           "050000005e040000050b0000ffff3f00", xbox_button_dpad,
           SDL_GAMEPAD_TYPE_XBOXONE,
           {fe::ControllerLayout::Xbox, false, false, true, "Xbox Elite Series 2",
            "guid"});
  CheckPad("Elite Series 2 (0B22) BLE", "030000005e040000220b000013050000",
           xbox_button_dpad, SDL_GAMEPAD_TYPE_XBOXONE,
           {fe::ControllerLayout::Xbox, false, false, true, "Xbox Elite Series 2",
            "guid"});

  std::cout << "\nPlayStation family\n";
  CheckPad("DualSense USB", "030000004c050000e60c000000007700", dualsense_mapping,
           SDL_GAMEPAD_TYPE_PS5,
           {fe::ControllerLayout::PlayStation, false, false, false, "DualSense",
            "guid"});
  CheckPad("DualSense Bluetooth", "050000004c050000e60c000000010000",
           dualsense_mapping, SDL_GAMEPAD_TYPE_PS5,
           {fe::ControllerLayout::PlayStation, false, false, false, "DualSense",
            "guid"});
  CheckPad("DualSense Edge USB", "030000004c050000f20d000000006800",
           dualsense_mapping, SDL_GAMEPAD_TYPE_PS5,
           {fe::ControllerLayout::PlayStation, false, false, true,
            "DualSense Edge", "guid"});
  CheckPad("DualSense Edge Bluetooth", "050000004c050000f20d000000010000",
           dualsense_mapping, SDL_GAMEPAD_TYPE_PS5,
           {fe::ControllerLayout::PlayStation, false, false, true,
            "DualSense Edge", "guid"});
  CheckPad("DualShock 3 with a>b raw order", "030000004c0500006802000000000000",
           "a:b2,b:b1,x:b0,y:b3,back:b8,dpdown:h0.4,dpleft:h0.8,dpright:h0.2,"
           "dpup:h0.1,start:b9,",
           SDL_GAMEPAD_TYPE_PS3,
           {fe::ControllerLayout::PlayStation, false, false, false, "DualShock 3",
            "guid"});
  CheckPad("PlayStation Classic with a>b raw order",
           "030000004c050000da0c000011010000",
           "a:b2,b:b1,x:b0,y:b3,back:b8,start:b9,",
           SDL_GAMEPAD_TYPE_PS3,
           {fe::ControllerLayout::PlayStation, false, false, false,
            "PlayStation gamepad", "guid-family"});

  std::cout << "\n8BitDo family\n";
  CheckPad("Pro 2 Nintendo mode (a>b)", "03000000c82d00000660000011010000",
           pro2_nintendo, SDL_GAMEPAD_TYPE_STANDARD,
           {fe::ControllerLayout::Switch, true, false, false,
            "8BitDo (Nintendo mapping)", "mapping"});
  CheckPad("Pro 2 standard/XInput mode (a<b)",
           "03000000c82d00000660000011010000", pro2_standard,
           SDL_GAMEPAD_TYPE_STANDARD,
           {fe::ControllerLayout::Generic, false, false, false, nullptr, nullptr});
  CheckPad("Pro 2 Switch mode (a>b, paddles)",
           "05000000ac05000004000000fd216d04", pro2_switch,
           SDL_GAMEPAD_TYPE_STANDARD,
           {fe::ControllerLayout::Switch, true, false, true, nullptr, "mapping"});
  CheckPad("Pro 2 reversed D-pad hat", "05000000ac05000004000000fd216d04",
           pro2_switch_reversed_hat, SDL_GAMEPAD_TYPE_STANDARD,
           {fe::ControllerLayout::Switch, true, true, true, nullptr, "mapping"});
  CheckPad("Ultimate standard order", "03000000c82d00001330000011010000",
           pro2_standard, SDL_GAMEPAD_TYPE_STANDARD,
           {fe::ControllerLayout::Generic, false, false, false, nullptr, nullptr});
  CheckPad("8BitDo 64 standard order", "03000000c82d00001930000000000000",
           pro2_standard, SDL_GAMEPAD_TYPE_STANDARD,
           {fe::ControllerLayout::Generic, false, false, false, nullptr, nullptr});

  std::cout << "\nNintendo Switch Pro\n";
  CheckPad("Switch Pro", "030000007e0500000920000000000000",
           "a:b1,b:b0,x:b3,y:b2,back:b9,dpdown:h0.4,dpleft:h0.8,"
           "dpright:h0.2,dpup:h0.1,start:b10,",
           SDL_GAMEPAD_TYPE_NINTENDO_SWITCH_PRO,
           {fe::ControllerLayout::Switch, true, false, false, "Switch Pro", "guid"});

  std::cout << "\nGenerated GCPadNew.ini bodies\n";
  {
    const std::string xbox = GenerateProfile("xbox", fe::ControllerLayout::Xbox);
    const std::string ps =
        GenerateProfile("playstation", fe::ControllerLayout::PlayStation);
    const std::string generic =
        GenerateProfile("generic", fe::ControllerLayout::Generic);
    const std::string sw =
        GenerateProfile("switch", fe::ControllerLayout::Switch);
    const std::string swapped =
        GenerateProfile("switch-dpad", fe::ControllerLayout::Switch, true);

    Check(!xbox.empty() && xbox == ps && xbox == generic,
          "Xbox, PlayStation and Generic generate one identical profile");
    Check(Has(xbox, "Buttons/A = `Button A`\n") &&
              Has(xbox, "Buttons/B = `Button B`\n") &&
              Has(xbox, "Buttons/X = `Button X`\n") &&
              Has(xbox, "Buttons/Y = `Button Y`\n") &&
              Has(xbox, "Buttons/Start = Start\n"),
          "Xbox profile uses the standard positional faces and Start");
    Check(Has(xbox, "D-Pad/Up = `Pad N`\n") &&
              Has(xbox, "D-Pad/Down = `Pad S`\n") &&
              Has(xbox, "D-Pad/Left = `Pad W`\n") &&
              Has(xbox, "D-Pad/Right = `Pad E`\n"),
          "Xbox profile leaves the D-pad in the SDL direction");
    Check(Has(sw, "Buttons/A = `Button B`\n") && Has(sw, "Buttons/B = `Button A`\n") &&
              Has(sw, "Buttons/X = `Button Y`\n") && Has(sw, "Buttons/Y = `Button X`\n"),
          "Switch profile swaps printed A/B and X/Y");
    Check(Has(swapped, "D-Pad/Left = `Pad E`\n") &&
              Has(swapped, "D-Pad/Right = `Pad W`\n"),
          "dpad_swap writes corrected Left/Right");
    Check(!Has(xbox, "addle") && !Has(ps, "addle") && !Has(sw, "addle"),
          "paddles and Fn/extra buttons never appear in a profile");
    Check(Has(xbox, "Device = SDL/0/Test Controller\n"),
          "profile keeps the exact SDL device string");
  }

  std::cout << "\nStartup diagnostic line\n";
  {
    const fe::ControllerDetection xbox =
        fe::DetectControllerMapping(SDL_GAMEPAD_TYPE_XBOX360,
                                    "030000005e0400008e02000000000000",
                                    xinput_mapping);
    const std::string line = fe::FormatControllerDiagnostic(
        "Xbox 360 Controller", "030000005e0400008e02000000000000", xbox);
    Check(Has(line, "guid=030000005e0400008e02000000000000") &&
              Has(line, "layout=xbox") && Has(line, "dpad_swap=no") &&
              Has(line, "face_swap=no") && Has(line, "family=\"Xbox 360\""),
          "Xbox diagnostic reports name/GUID/layout/dpad", line);
    std::cout << line << '\n';
  }

  std::cout << '\n' << g_checks << " checks, " << g_failures << " failed\n";
  return g_failures == 0 ? 0 : 1;
}