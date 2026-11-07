"""Contract checks for the texture-only 006 API route."""
import unittest, pathlib, json, tempfile, importlib.util, struct
api_path=pathlib.Path(__file__).resolve().parents[2]/'cel_kit_control.py'
spec=importlib.util.spec_from_file_location('veins_control',str(api_path));api=importlib.util.module_from_spec(spec);spec.loader.exec_module(api)

class VeinsControlTests(unittest.TestCase):
    def setUp(self):
        self.tmp=tempfile.TemporaryDirectory();self.p=pathlib.Path(self.tmp.name);source=self.p/'kit';source.mkdir()
        parts=[]
        for pid in ['wall_straight','wall_corner_out','wall_corner_in','wall_end','gate','cave_entrance','backdrop_rock','wall_lavafall']:
            f=pid+'_a.fbx';(source/f).write_bytes(b'protected 005 mesh')
            parts.append({'id':pid,'variant':'a','file':f,'triangles':12,'triangleBudget':600,'bounds':{'min':[-2,0,-1],'max':[2,5,1]}})
        self.kit={'styleId':api.LAVA_STYLE,'version':5,'parts':parts};api.write(source/'kit.json',self.kit)
        for f in ['atlas.png','cold_main_v1.png','cold_emissive_v1.png']:
            (self.p/f).write_bytes(b'\x89PNG\r\n\x1a\n'+b'\0'*8+struct.pack('>II',1024,1024))
        self.req={'apiVersion':1,'action':'plan','config':{'sourceKit':'kit','surfaceAtlas':'atlas.png','styleId':api.VEINS_STYLE,'coldMain':'cold_main_v1.png'}}
    def tearDown(self):self.tmp.cleanup()
    def test_new_style_advertised_alongside_previous_styles(self):
        ids=api.respond({'apiVersion':1,'action':'capabilities'})['result']['styleIds']
        self.assertEqual(set(ids),{api.STYLE,api.LAVA_STYLE,api.VEINS_STYLE})
    def test_texture_revision_keeps_part_count_and_triangle_count(self):
        r=api.respond(self.req,self.p);self.assertTrue(r['ok'],r)
        self.assertEqual(r['result']['parts'],8);self.assertEqual(r['result']['sourceTriangles'],96)
        self.assertTrue(r['result']['veinFeatures'])
    def test_wrong_source_version_is_rejected(self):
        self.kit['version']=6;api.write(self.p/'kit/kit.json',self.kit)
        r=api.respond(self.req,self.p);self.assertFalse(r['ok']);self.assertEqual(r['error']['code'],'invalid_config')
    def test_original_v1_cannot_be_repainted_as_006(self):
        self.kit['styleId']=api.STYLE;api.write(self.p/'kit/kit.json',self.kit)
        self.assertFalse(api.respond(self.req,self.p)['ok'])
    def test_restoration_reference_pair_is_required(self):
        (self.p/'cold_emissive_v1.png').unlink();self.assertFalse(api.respond(self.req,self.p)['ok'])
    def test_source_cannot_be_used_as_output(self):
        self.req.update(action='generate',outputFolder='kit');before=(self.p/'kit/kit.json').read_bytes()
        r=api.respond(self.req,self.p);self.assertEqual(r['error']['code'],'output_exists')
        self.assertEqual((self.p/'kit/kit.json').read_bytes(),before)

if __name__=='__main__':unittest.main(verbosity=2)
