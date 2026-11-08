"""Private, reversible stock FPJackieArms sleeve removal. No game data in Git.

Prepare from QuickBMS-extracted H/B FPJackieArms_skel.fbx. Install appends one
raw resource and atomically redirects its TOC entry; original cache bytes stay
intact. Rollback accepts only the recorded before/after states.
"""
import argparse
import ctypes
from ctypes import wintypes
import hashlib
import json
import os
from pathlib import Path
import struct

HEADER_SHA = '92080c173b952857f92ad275d776b53e58501e81b35a1bba03052901a8c85311'
BODY_SHA = 'cbbbdccd40daaa98c534c9790d2c681ea687ec007b1a9cf24356b720bc9fd679'
EXE_SHA = 'd46bb6cb762732b72ab9d34428237d2afa89f5792bdbed957d78793ee8134ec1'
RESOURCE = '/D2/Characters/Camera/FPJackieArms_skel.fbx'
VB_OFFSET, VB_SIZE, IB_OFFSET, INDEX_COUNT = 5894, 114400, 120294, 42909
ENTRY_OFFSET, ENTRY_SIZE = 91129648, 176296
ENTRY_SHA = '70590e21d645601e7168cd31d56f54c14df6dea35ce56f97cc53186c231b44a6'
PREVIOUS_CLOSED_WRIST_SHA = '71781234a1851232a74876504ccda0d8a86365dc358efcefc11d79454b47a936'


def sha(data):
    return hashlib.sha256(data).hexdigest()


def file_hash(path):
    h = hashlib.sha256()
    with open(path, 'rb') as f:
        for block in iter(lambda: f.read(4 * 1024 * 1024), b''):
            h.update(block)
    return h


def require(ok, message):
    if not ok:
        raise ValueError(message)


def ranges(header, label):
    require(header.count(label) == 1, 'Ambiguous mesh part')
    at = header.index(label)
    require(struct.unpack_from('<I', header, at - 4)[0] == len(label),
            'Unexpected native string length')
    table = struct.unpack_from('<10I', header, at + len(label))
    return list(zip(table[:5], table[5:]))


def patch(header, body):
    require(sha(header) == HEADER_SHA and sha(body) == BODY_SHA, 'Stock asset preimage mismatch')
    require(len(body) == IB_OFFSET + INDEX_COUNT * 2, 'Unexpected mesh layout')
    require(b'/D2/Characters/Jackie/JackieFPArmsHandRevised' in header and
            b'/D2/Characters/Jackie/JackieFPArmsSleeveRevised' in header, 'Wrong materials')
    hands = ranges(header, b'/HandRevised1')
    sleeves = ranges(header, b'/SleeveRevised')
    covered = set()
    for start, count in hands + sleeves:
        require(start % 3 == 0 and count % 3 == 0 and start + count <= INDEX_COUNT,
                'Invalid triangle range')
        r = set(range(start, start + count))
        require(not covered.intersection(r), 'Overlapping material ranges')
        covered.update(r)
    require(len(covered) == INDEX_COUNT, 'Unknown mesh range')
    output = bytearray(body)
    # Retain the first index of each triangle. All three remain inside the
    # original draw's vertex range. Degenerate triangles rasterize no sleeve.
    for start, count in sleeves:
        for index in range(start, start + count, 3):
            at = IB_OFFSET + 2 * index
            output[at + 2:at + 6] = output[at:at + 2] * 2
    require(output[:IB_OFFSET] == body[:IB_OFFSET], 'Vertex/skeleton mutation')
    for start, count in hands:
        a, b = IB_OFFSET + start * 2, IB_OFFSET + (start + count) * 2
        require(output[a:b] == body[a:b], 'Hand geometry mutation')
    return bytes(output), hands, sleeves
