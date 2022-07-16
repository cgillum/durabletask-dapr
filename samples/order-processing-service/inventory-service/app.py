from flask import Flask, request
import json
import os

app = Flask(__name__)


@app.route('/reserve-inventory', methods=['POST'])
def reserve_inventory():
    data = request.json
    print('Request received : ' + json.dumps(data), flush=True)
    return json.dumps({'success': True}), 200, {'ContentType': 'application/json'}


app.run(port=os.environ.get('APP_PORT', 5006))