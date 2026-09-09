import json
import subprocess

def submit():
    with open('pr_data.json', 'r') as f:
        data = json.load(f)

    # We can just write the content and we'll use the tool
    # Oh wait, we just use the `submit` tool from the MCP.
    pass

submit()
