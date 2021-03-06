const chai = require("chai");
const expect = chai.expect;
const dataprovider = require("../../data/dataprovider");
const app_register = require("../../behaviours/app_register_behaviour");
const post_keys = require("../../behaviours/post_keys_behaviour");
const testsSig = require("../../../util/sig_encoding");
const lab_confirm = require("../../behaviours/labconfirm_behaviour");
const manifest = require("../../behaviours/manifest_behaviour");
const exposure_key_set = require("../../behaviours/exposure_keys_set_behaviour");
const decode_protobuf = require("../../../util/protobuff_decoding");
const formatter = require("../../../util/format_strings");

describe("Validate push of my exposure key into manifest - #13_post_keys_to_manifest #scenario #regression", function () {
    this.timeout(3000 * 60 * 30);

    // console.log("Scenario: Register > Post keys > Lab Confirm > wait (x min.) > Manifest > EKS")

    let app_register_response,
        postkeys_response,
        lab_confirm_response,
        labConfirmationId,
        pollToken,
        manifest_response,
        manifest_response_updated,
        exposure_keyset_response,
        exposure_keyset_decoded,
        formated_bucket_id,
        exposureKeySet,
        exposure_keyset_decoded_set = [],
        currentVersion = "v1",
        nextVersion = "v2",
        payload,
        delayInMilliseconds = 10 * 60 * 1000 // delay should be minimal 10 min.

    beforeEach(done => setTimeout(done, 2000));

    before(function () {
        return manifest(nextVersion).then(function (manifest) {
            manifest_response = manifest;
        }).then(function () {
            return app_register(currentVersion)
                .then(function (register) {
                    app_register_response = register;
                    labConfirmationId = register.data.labConfirmationId;
                })
                .then(function () {
                    let map = new Map();
                    map.set("LABCONFIRMATIONID", formatter.format_remove_dash(labConfirmationId));

                    return lab_confirm(
                        dataprovider.get_data(
                            "lab_confirm_payload", "payload", "valid_dynamic_yesterday", map)
                        , currentVersion
                    ).then(function (confirm) {
                        lab_confirm_response = confirm;
                        pollToken = confirm.data.pollToken;
                    });
                })
                .then(function (sig) {
                    formated_bucket_id = formatter.format_remove_characters(app_register_response.data.bucketId);
                    let map = new Map();
                    map.set("BUCKETID", formated_bucket_id);

                    payload = dataprovider.get_data("post_keys_payload", "payload", "valid_dynamic_13_keys", map)
                    payload = JSON.parse(payload);
                    payload = JSON.stringify(payload);

                    return testsSig(
                        payload,
                        formatter.format_remove_characters(app_register_response.data.confirmationKey)
                    );
                })
                .then(function (sig) {
                    let map = new Map();
                    map.set("BUCKETID", formated_bucket_id);

                    return post_keys(
                        payload
                        , sig.sig
                        , currentVersion
                    ).then(function (postkeys) {
                        postkeys_response = postkeys;
                    });
                })
                .then(function () {
                    console.log(`Start delay for ${delayInMilliseconds / 1000 / 60} min.`)
                    console.log('started delay at: ' + new Date(Date.now()).toLocaleString());
                    return new Promise(function (resolve) {
                        setTimeout(function () {
                            resolve();
                        }, delayInMilliseconds);
                    })
                })
                .then(function () {
                    return manifest(nextVersion).then(function (manifest) {
                        manifest_response_updated = manifest;
                        exposureKeySet = manifest.content.exposureKeySets;
                    });
                })
                .then(async function () {

                    function getExposureKeySet(exposureKeySetId) {
                        return new Promise(function (resolve) {
                            exposure_key_set(exposureKeySetId, nextVersion).then(function (exposure_keyset) {
                                exposure_keyset_response = exposure_keyset;
                                return decode_protobuf(exposure_keyset_response)
                                    .then(function (EKS) {
                                        return resolve(exposure_keyset_decoded = EKS)
                                    });
                            })
                        });
                    }

                    for (i = 0; i < exposureKeySet.length; i++) {
                        let eks = await getExposureKeySet(exposureKeySet[i])
                        let TemporaryExposureKey = eks.keys
                        // decode keydata into readable text
                        TemporaryExposureKey.forEach(key => {
                            key.keyData = key.keyData.toString("base64");
                        })
                        let obj = {
                            "exposureKeySet": exposureKeySet[i],
                            "eks": eks
                        }
                        exposure_keyset_decoded_set.push(obj);
                    }
                });
        });
    });

    after(function () {
        dataprovider.clear_saved();
    })

    it("Etag and last-modified headers of manifest are updated after new key is added", function () {
        // console.log('headers etag: ' + manifest_response_updated.response.headers["etag"]);
        // console.log('headers last-modified: ' + manifest_response_updated.response.headers["last-modified"]);

        expect(manifest_response.response.headers["etag"], "New etag is found in manfifest header").to.not.equal(manifest_response_updated.response.headers["etag"])
        expect(manifest_response.response.headers["last-modified"], "New etag is found in manfifest header").to.not.equal(manifest_response_updated.response.headers["last-modified"])
    })

    it("The exposureKey pushed was in the manifest", function () {
        let exposure_key_send = JSON.parse(
            dataprovider.get_data("post_keys_payload", "payload", "valid_dynamic_13_keys", new Map())
        ).keys;

        let dateOfSymptomsOnset = JSON.parse(
            dataprovider.get_data(
                "lab_confirm_payload", "payload", "valid_dynamic_yesterday", new Map())
        ).dateOfSymptomsOnset;
        dateOfSymptomsOnset = new Date(dateOfSymptomsOnset)

        console.log('Number of exposure_keyset_decoded_set: ' + exposure_keyset_decoded_set.length);

        exposure_keyset_decoded_set.forEach(exposure_keyset_decoded => {
            console.log('Received key set: ' + exposure_keyset_decoded.exposureKeySet);

            exposure_keyset_decoded.eks.keys.forEach(received_keys => {
                exposure_key_send.forEach(send_keys => {

                    if (received_keys.keyData == send_keys.keyData) {
                        console.log('Key found in EKS: ' + exposure_keyset_decoded.exposureKeySet);
                        console.log('send_keys.keyData: ' + send_keys.keyData);
                        for (var i = 0; i < exposure_key_send.length; i++) {
                            if (exposure_key_send[i].keyData === send_keys.keyData) {
                                exposure_key_send.splice(i, 1);
                            }
                        }
                    }
                })
            })
        })

        // validate keys are found in manifest
        if (exposure_key_send.length === 0) {
            expect(exposure_key_send.length, `Expected EKS are found in the manifest`).to.be.equal(0);
        } else {
            let result = "";
            exposure_key_send.forEach(item => result += ` "RSN: ${item.rollingStartNumber}, keydata: ${item.keyData}" `)
            expect(exposure_key_send.length, `Expected EKS keys ${result} are NOT found in the manifest when DSSO is ${dateOfSymptomsOnset}`).to.be.equal(0);
        }
    });

});
