#!/usr/bin/python -B
import os

# inform automation publish scripts where to find our package, which is embedded inside of the UnityMocks
# project for easy development.
def packages_list():
    return [
        ("com.unity.mocks", os.path.join("UnityMocks", "Packages", "com.unity.mocks"))
    ]

if __name__ == "__main__":
    import sys
    sys.path.insert(0, os.path.abspath(os.path.join("..", "automation-tools")))
    
    try:
        import unity_package_build
        build_log = unity_package_build.setup()
    except ImportError:
        print "No Automation Tools found."
