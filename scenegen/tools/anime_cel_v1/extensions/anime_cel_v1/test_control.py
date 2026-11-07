import copy
import importlib.util
import json
import pathlib
import struct
import tempfile
import unittest

spec=importlib.util.spec_from_file_location('cel_control',str(pathlib.Path(__file__).resolve().parents[2]/'cel_kit_control.py'))
api=importlib.util.module_from_spec(spec); spec.loader.exec_module(api)


class CelControlContract(unittest.TestCase):
    def setUp(self):
        self.tmp=tempfile.TemporaryDirectory()
        self.base=pathlib.Path(self.tmp.name)
        (self.base/'source').mkdir()
        parts=[]
        for pid in ('wall_straight','wall_corner_out','wall_corner_in','wall_end','gate','cave_entrance','backdrop_rock'):
            name=pid+'_a.fbx'; (self.base/'source'/name).write_bytes(b'fbx fixture')
            parts.append({'id':pid,'variant':'a','file':name,'triangles':12,'triangleBudget':600,'bounds':{'min':[-2,0,-1],'max':[2,5,1]}})
        self.kit={'parts':parts}
        api.write(self.base/'source/kit.json',self.kit)
        (self.base/'atlas.png').write_bytes(b'\x89PNG\r\n\x1a\n'+b'\0'*8+struct.pack('>II',2048,2048))
        self.cfg={'sourceKit':'source','surfaceAtlas':'atlas.png','quality':'mobile'}
        self.req={'apiVersion':1,'action':'validate','requestId':'test','config':self.cfg}

    def tearDown(self):
        self.tmp.cleanup()

    def code(self,req):
        r=api.respond(req,self.base); self.assertFalse(r['ok']); return r['error']['code']

    def test_version_and_action_contract(self):
        self.assertEqual(self.code({'apiVersion':2,'action':'capabilities'}),'unsupported_version')
        self.assertEqual(self.code({'apiVersion':1,'action':'nonexistent'}),'unknown_action')

    def test_does_not_impersonate_whole_scene_api(self):
        r=api.respond({'apiVersion':1,'action':'capabilities'})
        self.assertTrue(r['ok']); self.assertFalse(r['result']['coreApiChanged'])

    def test_config_paths_are_relative_to_config_file(self):
        api.write(self.base/'config.json',self.cfg)
        elsewhere=self.base/'jobs'; elsewhere.mkdir()
        r=api.respond({'apiVersion':1,'action':'validate','configFile':'../config.json'},elsewhere)
        self.assertTrue(r['ok'],r); self.assertEqual(r['result']['parts'],7)

    def test_existing_output_is_never_overwritten(self):
        out=self.base/'existing'; out.mkdir(); sentinel=out/'keep.txt'; sentinel.write_text('keep')
        req=copy.deepcopy(self.req); req.update(action='generate',outputFolder=str(out))
        self.assertEqual(self.code(req),'output_exists'); self.assertEqual(sentinel.read_text(),'keep')

    def test_missing_geometry_and_traversal_rejected(self):
        self.kit['parts'][0]['file']='../atlas.png'; api.write(self.base/'source/kit.json',self.kit)
        self.assertEqual(self.code(self.req),'invalid_config')

    def test_bad_atlas_shape_rejected(self):
        (self.base/'atlas.png').write_bytes(b'\x89PNG\r\n\x1a\n'+b'\0'*8+struct.pack('>II',2048,1024))
        self.assertEqual(self.code(self.req),'invalid_config')

    def test_incomplete_generate_cannot_be_reported_successful(self):
        out=self.base/'broken'; out.mkdir(); api.write(out/'status.json',{'status':'complete'})
        self.assertEqual(self.code({'apiVersion':1,'action':'inspect','outputFolder':str(out)}),'invalid_output')

    def test_plan_tracks_source_bytes(self):
        q=copy.deepcopy(self.req); q['action']='plan'
        r1=api.respond(q,self.base); self.assertTrue(r1['ok'])
        (self.base/'source/wall_straight_a.fbx').write_bytes(b'changed mesh')
        r2=api.respond(q,self.base)
        self.assertNotEqual(r1['result']['sourcePartFiles'][0]['sha256'],r2['result']['sourcePartFiles'][0]['sha256'])


if __name__=='__main__': unittest.main(verbosity=2)
