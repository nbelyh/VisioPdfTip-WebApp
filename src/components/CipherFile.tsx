import { useState } from 'react';
import { DropZone } from './DropZone';
import { PrimaryButton } from './PrimaryButton';
import { AzureFunctionBackend } from '../services/AzureFunctionBackend';
import { useDotNetFixedUrl } from '../services/useDotNetFixedUrl';
import { ErrorNotification } from './ErrorNotification';

export const CipherFile = (props: {
}) => {

  const [vsdx, setVsdx] = useState<File>();
  const [error, setError] = useState('');
  const [processing, setProcessing] = useState(false);

  const onFileChange = (file?: File) => {
    setVsdx(file);
  }

  const { dotnet, loading, loadError } = useDotNetFixedUrl();

  const doProcessing = async (vsdx: File) => {
    var options = {
      enableCipherShapeText,
      enableCipherShapeFields,
      enableCipherPageNames,
      enableCipherPropertyValues,
      enableCipherPropertyLabels
    };
    const optionsJson = JSON.stringify(options);
    if (dotnet) {
      var ab = new Uint8Array(await vsdx.arrayBuffer());
      const output: Uint8Array = dotnet.FileProcessor.CipherFile(ab, optionsJson)
      return new Blob([output], { type: 'application/vnd.ms-visio.drawing' });
    } else {
      return await AzureFunctionBackend.invoke({ vsdx, optionsJson }, 'CipherFileAzureFunction');
    }
  }

  const onCipherFile = async () => {

    if (typeof window.appInsights !== 'undefined') {
      window.appInsights.trackEvent({ name: "SplitPagesClicked" });
    }

    setError('');

    if (!vsdx) {
      setError('Please select the VSDX file');
      return;
    }

    setProcessing(true);
    try {
      const blob = await doProcessing(vsdx);
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.target = "_blank";
      a.href = url;
      a.download = vsdx.name;
      a.click();
    } catch (e: any) {
      setError(`${e}`);
    } finally {
      setProcessing(false);
    }
  }

  const [enableCipherShapeText, setEnableCipherShapeText] = useState(true);
  const [enableCipherShapeFields, setEnableCipherShapeFields] = useState(true);
  const [enableCipherPageNames, setEnableCipherPageNames] = useState(true);
  const [enableCipherPropertyValues, setEnableCipherPropertyValues] = useState(true);
  const [enableCipherPropertyLabels, setEnableCipherPropertyLabels] = useState(false);

  return (
    <>
      <ErrorNotification error={error || loadError} />
      <DropZone
        accept="application/vnd.ms-visio.drawing"
        sampleFileName="Cipher.vsdx"
        label="Drop the Visio VSDX file to split pages here"
        onChange={onFileChange}
      />

      <div className='mb-4'>
        <div className="flex items-center">
          <input type="checkbox" className="rounded-sm mr-2" id="enableCipherText" checked={enableCipherShapeText} onChange={(e) => setEnableCipherShapeText(e.target.checked)} />
          <label htmlFor="enableCipherText">Cipher Shape Text</label>
        </div>

        <div className="flex items-center">
          <input type="checkbox" className="rounded-sm mr-2" id="enableCipherShapeFields" checked={enableCipherShapeFields} onChange={(e) => setEnableCipherShapeFields(e.target.checked)} />
          <label htmlFor="enableCipherShapeFields">Cipher Shape Fields</label>
        </div>

        <div className="flex items-center">
          <input type="checkbox" className="rounded-sm mr-2" id="enableCipherPageNames" checked={enableCipherPageNames} onChange={(e) => setEnableCipherPageNames(e.target.checked)} />
          <label htmlFor="enableCipherPageNames">Cipher Page Names</label>
        </div>

        <div className="flex items-center">
          <input type="checkbox" className="rounded-sm mr-2" id="enableCipherPropertyValues" checked={enableCipherPropertyValues} onChange={(e) => setEnableCipherPropertyValues(e.target.checked)} />
          <label htmlFor="enableCipherPropertyValues">Cipher Property Values</label>
        </div>

        <div className="flex items-center">
          <input type="checkbox" className="rounded-sm mr-2" id="enableCipherPropertyLabels" checked={enableCipherPropertyLabels} onChange={(e) => setEnableCipherPropertyLabels(e.target.checked)} />
          <label htmlFor="enableCipherPropertyLabels">Cipher Property Labels</label>
        </div>
      </div>

      <hr className="my-4" />

      <PrimaryButton disabled={!vsdx || processing || loading} onClick={onCipherFile}>Cipher</PrimaryButton>
    </>
  );
}