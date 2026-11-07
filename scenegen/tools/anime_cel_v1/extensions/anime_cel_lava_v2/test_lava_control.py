import unittest, pathlib, json, tempfile, importlib.util, copy, struct
api_path=pathlib.Path(__file__).resolve().parents[2]/'cel_kit_control.py'
spec=importlib.util.spec_from_file_location('lava_control',str(api_path));api=importlib.util.module_from_spec(spec);spec.loader.exec_module(api)

class LavaControlTests(unittest.TestCase):
    def setUp(self):
        self.tmp=tempfile.TemporaryDirectory();self.p=pathlib.Path(self.tmp.name);source=self.p/'kit';source.mkdir()
        parts=[]
        for id in ['wall_straight','wall_corner_out','wall_corner_in','wall_end','gate','cave_entrance','backdrop_rock']:
            f=id+'_a.fbx';(source/f).write_bytes(b'fbx fixture');parts.append({'id':id,'variant':'a','file':f,'triangles':12,'triangleBudget':600,'bounds':{'min':[-2,0,-1],'max':[2,5,1]}})
        self.kit={'styleId':api.STYLE,'parts':parts};api.write(source/'kit.json',self.kit)
        (self.p/'atlas.png').write_bytes(b'\x89PNG\r\n\x1a\n'+b'\0'*8+struct.pack('>II',1024,1024))
        self.req={'apiVersion':1,'action':'plan','config':{'sourceKit':'kit','surfaceAtlas':'atlas.png','styleId':api.LAVA_STYLE}}
    def tearDown(self):self.tmp.cleanup()
    def test_new_style_is_advertised_without_removing_v1(self):
        result=api.respond({'apiVersion':1,'action':'capabilities'})['result'];self.assertIn(api.STYLE,result['styleIds']);self.assertIn(api.LAVA_STYLE,result['styleIds']);self.assertFalse(result['coreApiChanged'])
    def test_plan_adds_two_falls_and_exposes_lava_requirements(self):
        r=api.respond(self.req,self.p);self.assertTrue(r['ok'],r);self.assertEqual(r['result']['parts'],9);self.assertEqual(len(r['result']['lavaFeatures']),6)
    def test_non_cel_source_is_rejected(self):
        self.kit['styleId']='other';api.write(self.p/'kit/kit.json',self.kit);r=api.respond(self.req,self.p);self.assertFalse(r['ok']);self.assertEqual(r['error']['code'],'invalid_config')
    def test_default_v1_plan_remains_unchanged(self):
        del self.req['config']['styleId'];r=api.respond(self.req,self.p);self.assertTrue(r['ok']);self.assertEqual(r['result']['parts'],7);self.assertEqual(r['result']['styleId'],api.STYLE)
    def test_existing_source_is_not_reused_as_output(self):
        self.req.update(action='generate',outputFolder='kit');before=(self.p/'kit/kit.json').read_bytes();r=api.respond(self.req,self.p);self.assertEqual(r['error']['code'],'output_exists');self.assertEqual((self.p/'kit/kit.json').read_bytes(),before)

if __name__=='__main__':unittest.main(verbosity=2)
